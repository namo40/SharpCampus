using Microsoft.Extensions.Options;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.Server.Common.Rooms;

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
                await registry.RegisterAsync(new RoomServerEntry(
                    settings.Name,
                    settings.ClientEndpoint,
                    settings.ControlEndpoint,
                    rooms.RoomCount,
                    settings.Capacity));
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

        // Leaving on the way out means the queue stops sending players here immediately rather than
        // once the entry expires.
        await registry.RemoveAsync(options.Value.Name);
    }
}
