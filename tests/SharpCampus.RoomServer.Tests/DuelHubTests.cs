using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

// End to end over the real hub, the real group and a real looper: two clients meet in a room and
// both learn how it ended.
//
// Each test gets its own host: the test server mishandles a duplex stream that is still tearing down
// when the next one connects, which a shared host would expose between test methods.
public sealed class DuelHubTests : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private readonly WebApplicationFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TwoClients_MeetInARoomAndBothSeeTheForfeit()
    {
        var first = new RecordingReceiver();
        var second = new RecordingReceiver();

        using var firstChannel = CreateChannel();
        using var secondChannel = CreateChannel();

        var cancellationToken = TestContext.Current.CancellationToken;
        var firstHub = await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
            firstChannel,
            first,
            cancellationToken: cancellationToken);
        var secondHub = await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
            secondChannel,
            second,
            cancellationToken: cancellationToken);

        try
        {
            var firstJoin = await firstHub.JoinAsync(new JoinRoomRequest("it-room", "first"));
            var secondJoin = await secondHub.JoinAsync(new JoinRoomRequest("it-room", "second"));

            Assert.Equal(new JoinRoomResult(true, new PlayerIndex(0), WaitingForOpponent: true), firstJoin);
            Assert.Equal(new JoinRoomResult(true, new PlayerIndex(1), WaitingForOpponent: false), secondJoin);

            var starting = await first.Starting.WaitAsync(_timeout, cancellationToken);
            await second.Starting.WaitAsync(_timeout, cancellationToken);

            Assert.Equal("first", starting.Players[0].DisplayName);
            Assert.Equal("second", starting.Players[1].DisplayName);

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
        using var channel = CreateChannel();
        var hub = await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
            channel,
            new RecordingReceiver(),
            cancellationToken: TestContext.Current.CancellationToken);

        try
        {
            var join = await hub.JoinAsync(new JoinRoomRequest("it-snapshot", "solo"));
            Assert.True(join.Accepted);

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

    private GrpcChannel CreateChannel() => GrpcChannel.ForAddress(
        _factory.Server.BaseAddress,
        new GrpcChannelOptions { HttpHandler = _factory.Server.CreateHandler() });
}
