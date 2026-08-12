using Cysharp.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Shared.Duel;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public class RoomManagerTests
{
    [Fact]
    public void GetOrCreate_ReturnsTheSameRoomForAKey()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = new RoomManager(pool, RoomFixture.Rules(), NullLogger<RoomManager>.Instance);

        var room = manager.GetOrCreate("alpha");

        Assert.Same(room, manager.GetOrCreate("alpha"));
        Assert.NotSame(room, manager.GetOrCreate("beta"));
        Assert.Equal(2, manager.RoomCount);
    }

    [Fact]
    public void RegisteredRoom_RunsOnTheLooperAndIsRemovedWhenItCloses()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = new RoomManager(pool, RoomFixture.Rules(joinTimeoutTicks: 3), NullLogger<RoomManager>.Instance);
        var room = manager.GetOrCreate("alpha");

        pool.Tick(2);

        Assert.Equal(1, manager.RoomCount);
        Assert.False(room.IsClosed);

        // The third tick spends the join timeout, which unregisters the action and drops the room.
        pool.Tick();

        Assert.True(room.IsClosed);
        Assert.Equal(0, manager.RoomCount);
    }

    [Fact]
    public void GetOrCreate_ReplacesARoomThatHasClosed()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = new RoomManager(pool, RoomFixture.Rules(joinTimeoutTicks: 1), NullLogger<RoomManager>.Instance);
        var closed = manager.GetOrCreate("alpha");

        pool.Tick();

        Assert.True(closed.IsClosed);
        Assert.NotSame(closed, manager.GetOrCreate("alpha"));
    }

    [Fact]
    public async Task RoomWhoseTickThrew_IsTornDownInsteadOfLingering()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = new RoomManager(pool, RoomFixture.Rules(), NullLogger<RoomManager>.Instance);
        var room = manager.GetOrCreate("alpha");

        await manager.ObserveAsync(room, Task.FromException(new InvalidOperationException("tick died")));

        Assert.True(room.IsClosed);
        Assert.Equal(0, manager.RoomCount);
    }

    [Fact]
    public void GetOrCreate_GivesEachRoomItsOwnSeed()
    {
        using var pool = new ManualLogicLooperPool(20);
        var manager = new RoomManager(pool, RoomFixture.Rules(), NullLogger<RoomManager>.Instance);

        var orders = new HashSet<string>();
        for (var i = 0; i < 8; i++)
        {
            orders.Add(PieceOrder(manager.GetOrCreate($"room-{i}")));
        }

        Assert.True(orders.Count > 1);
    }

    // The piece order is the only thing a seed shows through, so it stands in for the seed itself.
    private static string PieceOrder(DuelRoom room)
    {
        var completion = new TaskCompletionSource<DuelSnapshot>();
        room.TryPost(RoomCommand.Snapshot(completion));
        room.Tick();

        return string.Join(",", completion.Task.Result.Players[0].Next);
    }
}
