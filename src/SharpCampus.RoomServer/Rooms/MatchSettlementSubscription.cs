using MessagePipe;

namespace SharpCampus.RoomServer.Rooms;

// MessagePipe connects nothing on its own: a handler receives messages for exactly as long as the
// subscription it was given back stays undisposed, which is what ties it to the host's lifetime here.
internal sealed class MatchSettlementSubscription(
    IAsyncSubscriber<MatchFinishedEvent> matchFinished,
    MatchSettlementHandler handler) : IHostedService
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
            // A bot match closes its room the moment it ends, so a draining shutdown can get here with
            // the final settlement still on its way to the database. The host's shutdown timeout bounds
            // the wait.
            await _inFlight.WhenIdleAsync().WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown has run out of patience; whatever was written is written.
        }
    }
}
