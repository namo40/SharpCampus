using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Serialization;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

// End to end over the real hub, the real group and a real looper: clients enter a room the ApiServer
// would have asked for, carrying the tokens matchmaking would have issued them.
//
// Each test gets its own host: the test server mishandles a duplex stream that is still tearing down
// when the next one connects, which a shared host would expose between test methods.
public sealed class DuelHubTests : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private readonly RoomServerTestFactory _factory = new();
    private readonly List<GrpcChannel> _channels = [];
    private readonly Guid _firstUser = Guid.NewGuid();
    private readonly Guid _secondUser = Guid.NewGuid();

    public void Dispose()
    {
        foreach (var channel in _channels)
        {
            channel.Dispose();
        }

        _factory.Dispose();
    }

    [Fact]
    public async Task MatchedPair_MeetsInTheRoomAndBothSeeTheForfeit()
    {
        var roomId = CreateRoom();

        var first = new RecordingReceiver();
        var second = new RecordingReceiver();

        var cancellationToken = TestContext.Current.CancellationToken;
        var firstHub = await ConnectAsync(first, _firstUser, cancellationToken);
        var secondHub = await ConnectAsync(second, _secondUser, cancellationToken);

        try
        {
            var firstJoin = await firstHub.JoinAsync(Request(roomId, _firstUser));
            var secondJoin = await secondHub.JoinAsync(Request(roomId, _secondUser));

            Assert.Equal(new JoinRoomResult(true, new PlayerIndex(0), WaitingForOpponent: true), firstJoin);
            Assert.Equal(new JoinRoomResult(true, new PlayerIndex(1), WaitingForOpponent: false), secondJoin);

            var starting = await first.Starting.WaitAsync(_timeout, cancellationToken);
            await second.Starting.WaitAsync(_timeout, cancellationToken);

            Assert.Equal("alpha", starting.Players[0].DisplayName);
            Assert.Equal("beta", starting.Players[1].DisplayName);

            await firstHub.ForfeitAsync();

            var firstResult = await first.Ended.WaitAsync(_timeout, cancellationToken);
            var secondResult = await second.Ended.WaitAsync(_timeout, cancellationToken);

            Assert.Equal(DuelOutcome.Player2Wins, firstResult.Outcome);
            Assert.Equal(MatchEndReason.Forfeit, firstResult.Reason);
            Assert.Equal(firstResult, secondResult);
        }
        finally
        {
            await firstHub.DisposeAsync();
            await secondHub.DisposeAsync();
        }
    }

    [Fact]
    public async Task Snapshot_ReturnsBothBoardsToAWaitingPlayer()
    {
        var roomId = CreateRoom();

        var hub = await ConnectAsync(new RecordingReceiver(), _firstUser, TestContext.Current.CancellationToken);

        try
        {
            Assert.True((await hub.JoinAsync(Request(roomId, _firstUser))).Accepted);

            var snapshot = await hub.RequestSnapshotAsync();

            Assert.Equal(2, snapshot.Players.Length);
            Assert.Equal(DuelReplicaBoard.Width * DuelReplicaBoard.Height, snapshot.Players[0].Cells.Length);
            Assert.Equal(snapshot.Players[0].Next, snapshot.Players[1].Next);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConnectingWithoutAToken_IsRefused()
    {
        var failure = await Assert.ThrowsAsync<RpcException>(async () =>
            await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
                Channel(),
                new RecordingReceiver(),
                serializerProvider: ContractSerialization.Provider,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(StatusCode.Unauthenticated, failure.StatusCode);
    }

    [Fact]
    public async Task ForgedEntryToken_IsRefused()
    {
        var roomId = CreateRoom();
        var token = _factory.EntryTokens.Issue(new UserId(_firstUser), roomId);

        // Flip the last character of the signature, leaving the payload the room would accept.
        var forged = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        Assert.Equal(JoinRoomResult.Rejected, await JoinAsync(new JoinRoomRequest(roomId, forged), _firstUser));
    }

    [Fact]
    public async Task EntryTokenForAnotherRoom_IsRefused()
    {
        var roomId = CreateRoom();
        var otherRoomId = CreateRoom();

        var token = _factory.EntryTokens.Issue(new UserId(_firstUser), otherRoomId);

        Assert.Equal(JoinRoomResult.Rejected, await JoinAsync(new JoinRoomRequest(roomId, token), _firstUser));
    }

    [Fact]
    public async Task EntryTokenBelongingToAnotherAccount_IsRefused()
    {
        var roomId = CreateRoom();
        var token = _factory.EntryTokens.Issue(new UserId(_secondUser), roomId);

        // The token itself is valid, but it is not the account this connection signed in as.
        Assert.Equal(JoinRoomResult.Rejected, await JoinAsync(new JoinRoomRequest(roomId, token), _firstUser));
    }

    [Fact]
    public async Task EntryTokenForARoomThatDoesNotExist_IsRefused()
    {
        var unknownRoomId = new RoomId(Ulid.NewUlid());
        var token = _factory.EntryTokens.Issue(new UserId(_firstUser), unknownRoomId);

        Assert.Equal(JoinRoomResult.Rejected, await JoinAsync(new JoinRoomRequest(unknownRoomId, token), _firstUser));
    }

    [Fact]
    public async Task AccountTheRoomIsNotWaitingFor_IsRefused()
    {
        var roomId = CreateRoom();
        var stranger = Guid.NewGuid();
        var token = _factory.EntryTokens.Issue(new UserId(stranger), roomId);

        Assert.Equal(JoinRoomResult.Rejected, await JoinAsync(new JoinRoomRequest(roomId, token), stranger));
    }

    // Stands in for the ApiServer's room control call, which RoomControlServiceOverGrpcTests covers over
    // the wire. Reaching the manager directly keeps a unary request off the connections these tests
    // stream over, which the test server does not survive.
    private RoomId CreateRoom()
    {
        var roomId = new RoomId(Ulid.NewUlid());

        var outcome = _factory.Rooms.Create(roomId, [
            new RoomPlayer(new UserId(_firstUser), "alpha"),
            new RoomPlayer(new UserId(_secondUser), "beta"),
        ]);

        Assert.Equal(CreateRoomOutcome.Created, outcome);
        return roomId;
    }

    private async Task<JoinRoomResult> JoinAsync(JoinRoomRequest request, Guid connectedAs)
    {
        var hub = await ConnectAsync(new RecordingReceiver(), connectedAs, TestContext.Current.CancellationToken);

        try
        {
            return await hub.JoinAsync(request);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    private JoinRoomRequest Request(RoomId roomId, Guid userId)
        => new(roomId, _factory.EntryTokens.Issue(new UserId(userId), roomId));

    private Task<IDuelHub> ConnectAsync(RecordingReceiver receiver, Guid userId, CancellationToken cancellationToken)
        => StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
            Channel(),
            receiver,
            option: new CallOptions(new Metadata { { "authorization", $"Bearer {_factory.CreateToken(userId)}" } }),
            serializerProvider: ContractSerialization.Provider,
            cancellationToken: cancellationToken);

    // Channels outlive the calls made on them: closing one while the test server is still unwinding a
    // duplex stream is what the per-test host above exists to avoid.
    private GrpcChannel Channel()
    {
        var channel = _factory.CreateChannel();
        _channels.Add(channel);
        return channel;
    }
}
