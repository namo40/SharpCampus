using MessagePipe;

namespace SharpCampus.RoomServer.Rooms;

// A subscription of its own rather than a line in the settlement's: each handler starts and stops with
// nothing but itself at stake, which is what lets a second subscriber be added without touching the first.
internal sealed class MissionProgressSubscription(
    IAsyncSubscriber<MatchFinishedEvent> matchFinished,
    MissionProgressHandler handler) : IHostedService
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = matchFinished.Subscribe(handler);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }
}
