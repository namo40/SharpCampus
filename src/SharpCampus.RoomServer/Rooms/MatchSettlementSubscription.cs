using MessagePipe;

namespace SharpCampus.RoomServer.Rooms;

// MessagePipe connects nothing on its own: a handler receives messages for exactly as long as the
// subscription it was given back stays undisposed, which is what ties it to the host's lifetime here.
internal sealed class MatchSettlementSubscription(
    IAsyncSubscriber<MatchFinishedEvent> matchFinished,
    MatchSettlementHandler handler) : IHostedService
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
