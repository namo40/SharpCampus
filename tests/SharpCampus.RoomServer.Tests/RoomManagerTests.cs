using Cysharp.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public class RoomManagerTests
{
    [Fact]
    public void CreatedRoom_IsTheOneThatComesBackForItsId()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool);
        var roomId = new RoomId(Ulid.NewUlid());

        Assert.Equal(CreateRoomOutcome.Created, manager.Create(roomId, RoomFixture.Players()));
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
    public void SameRoomIdTwice_IsRefused()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool);
        var roomId = new RoomId(Ulid.NewUlid());

        manager.Create(roomId, RoomFixture.Players());

        Assert.Equal(CreateRoomOutcome.AlreadyExists, manager.Create(roomId, RoomFixture.Players()));
        Assert.Equal(1, manager.RoomCount);
    }

    [Fact]
    public void ServerAtCapacity_RefusesFurtherRooms()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool, capacity: 1);

        manager.Create(new RoomId(Ulid.NewUlid()), RoomFixture.Players());

        Assert.Equal(CreateRoomOutcome.AtCapacity, manager.Create(new RoomId(Ulid.NewUlid()), RoomFixture.Players()));
    }

    [Fact]
    public void RegisteredRoom_RunsOnTheLooperAndIsRemovedWhenItCloses()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool, joinTimeoutTicks: 3);
        var roomId = new RoomId(Ulid.NewUlid());
        manager.Create(roomId, RoomFixture.Players());

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
        var manager = CreateManager(pool, activeRooms: activeRooms);
        var roomId = new RoomId(Ulid.NewUlid());
        manager.Create(roomId, RoomFixture.Players());
        manager.TryGet(roomId, out var room);

        await manager.ObserveAsync(room!, Task.FromException(new InvalidOperationException("tick died")));

        Assert.True(room!.IsClosed);
        Assert.Equal(0, manager.RoomCount);
        await activeRooms.Received(1).ReleaseAsync(RoomFixture.FirstUser);
        await activeRooms.Received(1).ReleaseAsync(RoomFixture.SecondUser);
    }

    [Fact]
    public void EachRoomGetsItsOwnSeed()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = CreateManager(pool);

        var orders = new HashSet<string>();
        for (var i = 0; i < 8; i++)
        {
            var roomId = new RoomId(Ulid.NewUlid());
            manager.Create(roomId, RoomFixture.Players());
            manager.TryGet(roomId, out var room);
            orders.Add(PieceOrder(room!));
        }

        Assert.True(orders.Count > 1);
    }

    [Fact]
    public void ClosedRoom_LetsBothPlayersQueueAgain()
    {
        using var pool = new ManualLogicLooperPool(20);
        var activeRooms = RoomFixture.ActiveRooms();
        var manager = CreateManager(pool, joinTimeoutTicks: 2, activeRooms: activeRooms);
        manager.Create(new RoomId(Ulid.NewUlid()), RoomFixture.Players());

        pool.Tick(2);

        activeRooms.Received(1).ReleaseAsync(RoomFixture.FirstUser);
        activeRooms.Received(1).ReleaseAsync(RoomFixture.SecondUser);
    }

    private static RoomManager CreateManager(
        ILogicLooperPool pool,
        int capacity = 100,
        int joinTimeoutTicks = 5,
        IActiveRoomStore? activeRooms = null)
        => new(
            pool,
            RoomFixture.Rules(joinTimeoutTicks: joinTimeoutTicks),
            RoomFixture.MasterData(),
            activeRooms ?? RoomFixture.ActiveRooms(),
            RoomFixture.Options(capacity),
            RoomFixture.Publisher(),
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
