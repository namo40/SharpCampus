using Cysharp.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public class RoomManagerTests
{
    [Fact]
    public async Task CreatedRoom_IsTheOneThatComesBackForItsId()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool);
        var roomId = new RoomId(Ulid.NewUlid());

        Assert.Equal(CreateRoomOutcome.Created, await manager.CreateAsync(roomId, RoomFixture.Players()));
        Assert.True(manager.TryGet(roomId, out var room));
        Assert.NotNull(room);
        Assert.Equal(roomId, room.RoomId);
        Assert.Equal(1, manager.RoomCount);
    }

    [Fact]
    public void UnknownRoomId_HasNoRoom()
    {
        using var pool = new ManualLogicLooperPool(20);

        Assert.False(CreateManager(pool).TryGet(new RoomId(Ulid.NewUlid()), out _));
    }

    [Fact]
    public async Task SameRoomIdTwice_IsRefused()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool);
        var roomId = new RoomId(Ulid.NewUlid());

        await manager.CreateAsync(roomId, RoomFixture.Players());

        Assert.Equal(CreateRoomOutcome.AlreadyExists, await manager.CreateAsync(roomId, RoomFixture.Players()));
        Assert.Equal(1, manager.RoomCount);
    }

    [Fact]
    public async Task ServerAtCapacity_RefusesFurtherRooms()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool, capacity: 1);

        await manager.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players());

        Assert.Equal(
            CreateRoomOutcome.AtCapacity,
            await manager.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players()));
    }

    [Fact]
    public async Task RegisteredRoom_RunsOnTheLooperAndIsRemovedWhenItCloses()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool, joinTimeoutTicks: 3);
        var roomId = new RoomId(Ulid.NewUlid());
        await manager.CreateAsync(roomId, RoomFixture.Players());

        pool.Tick(2);

        Assert.Equal(1, manager.RoomCount);
        Assert.True(manager.TryGet(roomId, out var room));

        // The third tick spends the join timeout, which unregisters the action and drops the room.
        pool.Tick();

        Assert.True(room!.IsClosed);
        Assert.Equal(0, manager.RoomCount);
    }

    [Fact]
    public async Task RoomWhoseTickThrew_IsTornDownAndLetsItsPlayersQueueAgain()
    {
        using var pool = new ManualLogicLooperPool(20);
        var activeRooms = RoomFixture.ActiveRooms();
        var locations = RoomFixture.Locations();
        var manager = CreateManager(pool, activeRooms: activeRooms, locations: locations);
        var roomId = new RoomId(Ulid.NewUlid());
        await manager.CreateAsync(roomId, RoomFixture.Players());
        manager.TryGet(roomId, out var room);

        await manager.ObserveAsync(room!, Task.FromException(new InvalidOperationException("tick died")));

        Assert.True(room!.IsClosed);
        Assert.Equal(0, manager.RoomCount);
        await activeRooms.Received(1).ReleaseAsync(RoomFixture.FirstUser);
        await activeRooms.Received(1).ReleaseAsync(RoomFixture.SecondUser);
        await locations.Received(1).RemoveAsync(roomId);
    }

    [Fact]
    public async Task EachRoom_GetsASeedOfItsOwn()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool);

        var orders = new HashSet<string>();
        for (var i = 0; i < 8; i++)
        {
            var roomId = new RoomId(Ulid.NewUlid());
            await manager.CreateAsync(roomId, RoomFixture.Players());
            manager.TryGet(roomId, out var room);
            orders.Add(PieceOrder(room!));
        }

        Assert.True(orders.Count > 1);
    }

    [Fact]
    public async Task ClosedRoom_LetsBothPlayersQueueAgain()
    {
        using var pool = new ManualLogicLooperPool(20);
        var activeRooms = RoomFixture.ActiveRooms();
        var manager = CreateManager(pool, joinTimeoutTicks: 2, activeRooms: activeRooms);
        await manager.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players());

        pool.Tick(2);

        await activeRooms.Received(1).ReleaseAsync(RoomFixture.FirstUser);
        await activeRooms.Received(1).ReleaseAsync(RoomFixture.SecondUser);
    }

    [Fact]
    public async Task CreatedRoom_IsFiledUnderTheServerThatHoldsIt()
    {
        using var pool = new ManualLogicLooperPool(20);
        var locations = RoomFixture.Locations();
        var manager = CreateManager(pool, locations: locations);
        var roomId = new RoomId(Ulid.NewUlid());

        await manager.CreateAsync(roomId, RoomFixture.Players());

        await locations.Received(1).StoreAsync(roomId, "test");
    }

    [Fact]
    public async Task RoomThatWasRefused_IsFiledNowhere()
    {
        using var pool = new ManualLogicLooperPool(20);
        var locations = RoomFixture.Locations();
        var manager = CreateManager(pool, capacity: 1, locations: locations);

        await manager.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players());
        var refused = new RoomId(Ulid.NewUlid());
        await manager.CreateAsync(refused, RoomFixture.Players());

        await locations.DidNotReceive().StoreAsync(refused, Arg.Any<string>());
    }

    [Fact]
    public async Task Creation_IsNotAnsweredUntilTheRoomCanBeRoutedTo()
    {
        using var pool = new ManualLogicLooperPool(20);
        var filed = new TaskCompletionSource();
        var locations = RoomFixture.Locations();
        locations.StoreAsync(Arg.Any<RoomId>(), Arg.Any<string>()).Returns(filed.Task);
        var manager = CreateManager(pool, locations: locations);

        var creating = manager.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players());

        // The ticket the caller hands out names a server, so the answer has to wait for the entry it names.
        Assert.False(creating.IsCompleted);

        filed.SetResult();

        Assert.Equal(CreateRoomOutcome.Created, await creating);
    }

    [Fact]
    public async Task ClosedRoom_StopsBeingRoutedTo()
    {
        using var pool = new ManualLogicLooperPool(20);
        var locations = RoomFixture.Locations();
        var manager = CreateManager(pool, joinTimeoutTicks: 2, locations: locations);
        var roomId = new RoomId(Ulid.NewUlid());
        await manager.CreateAsync(roomId, RoomFixture.Players());

        pool.Tick(2);

        await locations.Received(1).RemoveAsync(roomId);
    }

    private static RoomManager CreateManager(
        ILogicLooperPool pool,
        int capacity = 100,
        int joinTimeoutTicks = 5,
        IActiveRoomStore? activeRooms = null,
        IRoomLocationStore? locations = null)
        => new(
            pool,
            RoomFixture.Rules(joinTimeoutTicks: joinTimeoutTicks),
            RoomFixture.MasterData(),
            activeRooms ?? RoomFixture.ActiveRooms(),
            locations ?? RoomFixture.Locations(),
            RoomFixture.Options(capacity),
            RoomFixture.Publisher(),
            RoomFixture.Metrics(),
            NullLogger<RoomManager>.Instance);

    // The piece order is the only thing a seed shows through, so it stands in for the seed itself.
    private static string PieceOrder(DuelRoom room)
    {
        var completion = new TaskCompletionSource<DuelSnapshot>();
        room.TryPost(RoomCommand.Snapshot(completion));
        room.Tick();

        return string.Join(",", completion.Task.Result.Players[0].Next);
    }
}
