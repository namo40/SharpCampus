using MessagePipe;

namespace SharpCampus.RoomServer.Rooms;

// A subscription of its own rather than a line in the settlement's: each handler starts and stops with
// nothing but itself at stake, which is what lets a second subscriber be added without touching the first.
internal sealed class MissionProgressSubscription(
    IAsyncSubscriber<MatchFinishedEvent> matchFinished,
    MissionProgressHandler handler) : IHostedService
{
    private readonly InFlightHandler<MatchFinishedEvent> _inFlight = new(handler);
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = matchFinished.Subscribe(_inFlight);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();

        try
        {
            // Same wait as the settlement's: this subscriber's writes are its own, and so is the last
            // one still in flight when shutdown reaches here.
            await _inFlight.WhenIdleAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown has run out of patience; whatever was written is written.
        }
    }
}
