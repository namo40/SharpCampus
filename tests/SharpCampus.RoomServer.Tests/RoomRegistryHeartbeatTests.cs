using Cysharp.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Shared.Values;
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

    private static RoomManager CreateRooms(ILogicLooperPool pool, IOptions<RoomServerOptions> options)
        => new(pool, RoomFixture.Rules(), RoomFixture.ActiveRooms(), options, NullLogger<RoomManager>.Instance);

    private RoomRegistryHeartbeat CreateHeartbeat(RoomManager rooms, IOptions<RoomServerOptions> options)
        => new(_registry, rooms, options, NullLogger<RoomRegistryHeartbeat>.Instance);
}
