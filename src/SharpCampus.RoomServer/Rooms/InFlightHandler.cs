using MessagePipe;

namespace SharpCampus.RoomServer.Rooms;

// The publisher fires and forgets on the loop thread, so once a room closes nothing holds a reference
// to the settlement still being written. This wrapper is that reference: whoever owns the subscription
// can wait for the work the messages started, which is what keeps a drained shutdown from cutting off
// the last match's write mid-flight.
internal sealed class InFlightHandler<T>(IAsyncMessageHandler<T> handler) : IAsyncMessageHandler<T>
{
    private readonly Lock _gate = new();
    private int _running;
    private TaskCompletionSource? _idle;

    public async ValueTask HandleAsync(T message, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _running++;
        }

        try
        {
            await handler.HandleAsync(message, cancellationToken);
        }
        finally
        {
            lock (_gate)
            {
                if (--_running == 0)
                {
                    _idle?.TrySetResult();
                }
            }
        }
    }

    public Task WhenIdleAsync()
    {
        lock (_gate)
        {
            if (_running == 0)
            {
                return Task.CompletedTask;
            }

            _idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _idle.Task;
        }
    }
}
