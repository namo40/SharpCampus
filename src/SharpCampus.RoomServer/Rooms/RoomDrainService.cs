using Microsoft.Extensions.Options;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.Server.Common.Rooms;
using StackExchange.Redis;

namespace SharpCampus.RoomServer.Rooms;

// StoppingAsync runs for every hosted service before the first StopAsync does, and Kestrel is stopped by
// a StopAsync of its own: this is the last moment in shutdown where the matches still have their streams,
// so it is the only place a wait for them means anything. The host's shutdown timeout bounds the whole of
// it, hence the shorter timeout of our own below.
internal sealed class RoomDrainService(
    RoomManager rooms,
    IRoomRegistry registry,
    IOptions<RoomServerOptions> options,
    ILogger<RoomDrainService> logger) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        rooms.BeginDrain();

        try
        {
            // Left before anything is waited for: whatever becomes of the rooms already here, the pairing
            // worker stops choosing this instance now rather than when the entry expires.
            await registry.RemoveAsync(options.Value.Name);
        }
        catch (Exception exception) when (exception is RedisConnectionException or RedisTimeoutException)
        {
            // With Redis gone there is nothing to leave from; the TTL retires the entry.
            logger.HeartbeatSkipped(exception.Message);
        }

        var timeout = options.Value.DrainTimeoutSeconds;
        var playing = rooms.RoomCount;
        if (timeout <= 0 || playing == 0)
        {
            return;
        }

        logger.DrainStarted(playing);

        try
        {
            await rooms.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(timeout), cancellationToken);
            logger.DrainCompleted(playing);
        }
        catch (Exception exception) when (exception is TimeoutException or OperationCanceledException)
        {
            // Shutdown carries on regardless: throwing here would only trade an interrupted match for an
            // interrupted match and a stack trace.
            // Production note: whatever is still playing is cut off when the grace period ends and the
            // process is killed. Matches with no bound on their length would have to be moved, not waited out.
            logger.DrainTimedOut(rooms.RoomCount);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
