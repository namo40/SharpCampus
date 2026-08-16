using Microsoft.Extensions.Options;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.Server.Common.Rooms;
using StackExchange.Redis;

namespace SharpCampus.RoomServer.Rooms;

// The registry entry expires on its own, so staying in it means saying so every few seconds. That is
// also what tells the pairing worker how loaded this instance is.
internal sealed class RoomRegistryHeartbeat(
    IRoomRegistry registry,
    RoomManager rooms,
    IOptions<RoomServerOptions> options,
    ILogger<RoomRegistryHeartbeat> logger) : BackgroundService
{
    private static readonly TimeSpan _period = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_period);

        var settings = options.Value;
        logger.RoomServerRegistered(settings.Name, rooms.RoomCount, settings.Capacity);

        try
        {
            do
            {
                // A beat that cannot reach Redis is a beat missed, not a dead server: an exception
                // leaving a BackgroundService stops the whole host, and the entry only expires if
                // Redis outlives its TTL anyway.
                try
                {
                    await registry.RegisterAsync(new RoomServerEntry(
                        settings.Name,
                        settings.ClientEndpoint,
                        settings.ControlEndpoint,
                        rooms.RoomCount,
                        settings.Capacity));
                }
                catch (Exception exception) when (exception is RedisConnectionException or RedisTimeoutException)
                {
                    logger.HeartbeatSkipped(exception.Message);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        try
        {
            // Leaving on the way out means the queue stops sending players here immediately rather than
            // once the entry expires.
            await registry.RemoveAsync(options.Value.Name);
        }
        catch (Exception exception) when (exception is RedisConnectionException or RedisTimeoutException)
        {
            // With Redis gone there is nothing to leave from; the TTL retires the entry.
            logger.HeartbeatSkipped(exception.Message);
        }
    }
}
