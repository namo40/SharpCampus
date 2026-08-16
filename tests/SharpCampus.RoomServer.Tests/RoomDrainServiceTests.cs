using Cysharp.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public class RoomDrainServiceTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private readonly IRoomRegistry _registry = Substitute.For<IRoomRegistry>();

    [Fact]
    public async Task Stopping_LeavesTheRegistryAndTurnsRoomsAway()
    {
        using var pool = new ManualLogicLooperPool(20);
        var options = RoomFixture.Options();
        var rooms = CreateRooms(pool, options);

        await CreateService(rooms, options).StoppingAsync(TestContext.Current.CancellationToken);

        Assert.True(rooms.IsDraining);
        await _registry.Received(1).RemoveAsync("test");
        Assert.Equal(
            CreateRoomOutcome.Draining,
            await rooms.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players()));
    }

    [Fact]
    public async Task StoppingWithoutADrainTimeout_WaitsForNothing()
    {
        using var pool = new ManualLogicLooperPool(20);
        var options = RoomFixture.Options();
        var rooms = CreateRooms(pool, options);
        await rooms.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players());

        await CreateService(rooms, options)
            .StoppingAsync(TestContext.Current.CancellationToken)
            .WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(1, rooms.RoomCount);
    }

    [Fact]
    public async Task DrainThatRunsOutOfTime_LetsShutdownCarryOn()
    {
        using var pool = new ManualLogicLooperPool(20);
        var options = RoomFixture.Options(drainTimeoutSeconds: 1);
        var rooms = CreateRooms(pool, options);

        // Nothing ticks this pool, so the room stays where it is for as long as the drain waits.
        await rooms.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players());

        await CreateService(rooms, options)
            .StoppingAsync(TestContext.Current.CancellationToken)
            .WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(1, rooms.RoomCount);
    }

    [Fact]
    public async Task RoomThatFinishesDuringTheDrain_EndsTheWait()
    {
        using var pool = new ManualLogicLooperPool(20);
        var options = RoomFixture.Options(drainTimeoutSeconds: 30);
        var rooms = CreateRooms(pool, options, joinTimeoutTicks: 2);
        await rooms.CreateAsync(new RoomId(Ulid.NewUlid()), RoomFixture.Players());

        var stopping = CreateService(rooms, options).StoppingAsync(TestContext.Current.CancellationToken);

        Assert.False(stopping.IsCompleted);

        // The join timeout closes the room, which is what the drain is waiting on.
        pool.Tick(2);

        await stopping.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Assert.Equal(0, rooms.RoomCount);
    }

    private static RoomManager CreateRooms(
        ILogicLooperPool pool,
        IOptions<RoomServerOptions> options,
        int joinTimeoutTicks = 5)
        => new(
            pool,
            RoomFixture.Rules(joinTimeoutTicks: joinTimeoutTicks),
            RoomFixture.MasterData(),
            RoomFixture.ActiveRooms(),
            RoomFixture.Locations(),
            options,
            RoomFixture.Publisher(),
            RoomFixture.Metrics(),
            NullLogger<RoomManager>.Instance);

    private RoomDrainService CreateService(RoomManager rooms, IOptions<RoomServerOptions> options)
        => new(rooms, _registry, options, NullLogger<RoomDrainService>.Instance);
}
