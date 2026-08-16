using Cysharp.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Shared.Values;
using StackExchange.Redis;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public class RoomRegistryHeartbeatTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    private readonly IRoomRegistry _registry = Substitute.For<IRoomRegistry>();

    [Fact]
    public async Task Heartbeat_RegistersBeforeWaitingOutTheFirstPeriod()
    {
        var registered = new TaskCompletionSource<RoomServerEntry>(TaskCreationOptions.RunContinuationsAsynchronously);
        _registry.When(registry => registry.RegisterAsync(Arg.Any<RoomServerEntry>()))
            .Do(call => registered.TrySetResult(call.Arg<RoomServerEntry>()));

        using var pool = new ManualLogicLooperPool(20);
        var options = RoomFixture.Options(capacity: 42);
        var rooms = CreateRooms(pool, options);
        rooms.Create(new RoomId(Ulid.NewUlid()), RoomFixture.Players());

        var cancellationToken = TestContext.Current.CancellationToken;
        var heartbeat = CreateHeartbeat(rooms, options);

        await heartbeat.StartAsync(cancellationToken);

        try
        {
            var entry = await registered.Task.WaitAsync(_timeout, cancellationToken);

            Assert.Equal("test", entry.Name);
            Assert.Equal("http://localhost:5002", entry.ClientEndpoint);
            Assert.Equal("http://localhost:5002", entry.ControlEndpoint);
            Assert.Equal(1, entry.RoomCount);
            Assert.Equal(42, entry.Capacity);
        }
        finally
        {
            await heartbeat.StopAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Shutdown_TakesTheServerOutOfTheRegistry()
    {
        using var pool = new ManualLogicLooperPool(20);
        var options = RoomFixture.Options();
        var rooms = CreateRooms(pool, options);

        var cancellationToken = TestContext.Current.CancellationToken;
        var heartbeat = CreateHeartbeat(rooms, options);

        await heartbeat.StartAsync(cancellationToken);
        await heartbeat.StopAsync(cancellationToken);

        await _registry.Received(1).RemoveAsync("test");
    }

    [Fact]
    public async Task BeatWithRedisGone_IsMissedInsteadOfKillingTheServer()
    {
        var beaten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _registry.RegisterAsync(Arg.Any<RoomServerEntry>()).Returns(_ =>
        {
            beaten.TrySetResult();
            return Task.FromException(RedisDown());
        });

        using var pool = new ManualLogicLooperPool(20);
        var options = RoomFixture.Options();
        var cancellationToken = TestContext.Current.CancellationToken;
        var heartbeat = CreateHeartbeat(CreateRooms(pool, options), options);

        await heartbeat.StartAsync(cancellationToken);

        try
        {
            await beaten.Task.WaitAsync(_timeout, cancellationToken);

            // The failed beat needs a moment to travel from the registry call to wherever it would land.
            await Task.Delay(100, cancellationToken);

            Assert.False(heartbeat.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await heartbeat.StopAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task ShutdownWithRedisGone_StillGetsToTheEnd()
    {
        _registry.RemoveAsync(Arg.Any<string>()).ThrowsAsync(RedisDown());

        using var pool = new ManualLogicLooperPool(20);
        var options = RoomFixture.Options();
        var cancellationToken = TestContext.Current.CancellationToken;
        var heartbeat = CreateHeartbeat(CreateRooms(pool, options), options);

        await heartbeat.StartAsync(cancellationToken);
        await heartbeat.StopAsync(cancellationToken);

        await _registry.Received(1).RemoveAsync("test");
    }

    private static RedisConnectionException RedisDown() =>
        new(ConnectionFailureType.UnableToConnect, CommandFlags.None, "down");

    private static RoomManager CreateRooms(ILogicLooperPool pool, IOptions<RoomServerOptions> options)
        => new(
            pool,
            RoomFixture.Rules(),
            RoomFixture.MasterData(),
            RoomFixture.ActiveRooms(),
            options,
            RoomFixture.Publisher(),
            RoomFixture.Metrics(),
            NullLogger<RoomManager>.Instance);

    private RoomRegistryHeartbeat CreateHeartbeat(RoomManager rooms, IOptions<RoomServerOptions> options)
        => new(_registry, rooms, options, NullLogger<RoomRegistryHeartbeat>.Instance);
}
