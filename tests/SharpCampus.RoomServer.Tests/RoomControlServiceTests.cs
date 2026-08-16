using Cysharp.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public class RoomControlServiceTests
{
    [Fact]
    public async Task CreateRoom_StandsUpARoomTheHubCanFind()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool);
        var roomId = new RoomId(Ulid.NewUlid());

        var result = await new RoomControlService(manager)
            .CreateRoomAsync(new CreateRoomRequest(roomId, RoomFixture.Players()));

        Assert.Equal(CreateRoomOutcome.Created, result.Outcome);
        Assert.True(manager.TryGet(roomId, out var room));
        Assert.True(room!.IsExpected(RoomFixture.FirstUser));
        Assert.True(room.IsExpected(RoomFixture.SecondUser));
    }

    [Fact]
    public async Task CreateRoom_IsRefusedOnceTheServerIsFull()
    {
        using var pool = new ManualLogicLooperPool(20);
        var service = new RoomControlService(CreateManager(pool, capacity: 1));

        await service.CreateRoomAsync(new CreateRoomRequest(new RoomId(Ulid.NewUlid()), RoomFixture.Players()));
        var result = await service.CreateRoomAsync(
            new CreateRoomRequest(new RoomId(Ulid.NewUlid()), RoomFixture.Players()));

        Assert.Equal(CreateRoomOutcome.AtCapacity, result.Outcome);
    }

    private static RoomManager CreateManager(ILogicLooperPool pool, int capacity = 100)
        => new(
            pool,
            RoomFixture.Rules(),
            RoomFixture.MasterData(),
            RoomFixture.ActiveRooms(),
            RoomFixture.Locations(),
            RoomFixture.Options(capacity),
            RoomFixture.Publisher(),
            RoomFixture.Metrics(),
            NullLogger<RoomManager>.Instance);
}
