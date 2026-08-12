using MagicOnion.Client;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Serialization;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

// The ApiServer's half of the flow: an ordinary MagicOnion Unary call between two of our own servers,
// carrying contract types the standard MessagePack resolver could not format on its own.
public sealed class RoomControlServiceOverGrpcTests : IDisposable
{
    private readonly RoomServerTestFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CreateRoom_StandsUpTheRoomTheClientsWillEnter()
    {
        var roomId = new RoomId(Ulid.NewUlid());
        var first = new UserId(Guid.NewGuid());
        var second = new UserId(Guid.NewGuid());

        using var channel = _factory.CreateChannel();
        var client = MagicOnionClient.Create<IRoomControlService>(channel, ContractSerialization.Provider);

        var result = await client.CreateRoomAsync(new CreateRoomRequest(roomId, [
            new RoomPlayer(first, "alpha"),
            new RoomPlayer(second, "beta"),
        ]));

        Assert.Equal(CreateRoomOutcome.Created, result.Outcome);
        Assert.True(_factory.Rooms.TryGet(roomId, out var room));
        Assert.True(room!.IsExpected(first));
        Assert.True(room.IsExpected(second));
    }

    [Fact]
    public async Task CreateRoom_TwiceUnderTheSameIdIsRefused()
    {
        var roomId = new RoomId(Ulid.NewUlid());
        var players = new[] { new RoomPlayer(new UserId(Guid.NewGuid()), "alpha") };

        using var channel = _factory.CreateChannel();
        var client = MagicOnionClient.Create<IRoomControlService>(channel, ContractSerialization.Provider);

        await client.CreateRoomAsync(new CreateRoomRequest(roomId, players));
        var result = await client.CreateRoomAsync(new CreateRoomRequest(roomId, players));

        Assert.Equal(CreateRoomOutcome.AlreadyExists, result.Outcome);
    }

    [Fact]
    public async Task CreateRoom_NeedsNoBearerToken()
    {
        // Server to server only: nothing outside the cluster can reach this, so there is no caller to
        // authenticate. RoomControlServiceOverGrpcTests connects without one and is served.
        using var channel = _factory.CreateChannel();
        var client = MagicOnionClient.Create<IRoomControlService>(channel, ContractSerialization.Provider);

        var result = await client.CreateRoomAsync(new CreateRoomRequest(
            new RoomId(Ulid.NewUlid()),
            [new RoomPlayer(new UserId(Guid.NewGuid()), "alpha")]));

        Assert.Equal(CreateRoomOutcome.Created, result.Outcome);
    }
}
