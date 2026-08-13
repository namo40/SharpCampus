using SharpCampus.Shared.Duel;

namespace SharpCampus.Cli;

// Everything a duel room pushes arrives on the hub's receive loop, while the command thread applies
// the snapshot it asked for and the render loop reads the boards; the lock is what keeps all three
// off each other's replica.
internal sealed class DuelReceiver : IDuelHubReceiver
{
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource<MatchStartInfo> _starting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<MatchResult> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DuelReplica Replica { get; } = new();

    public Task<MatchStartInfo> Starting => _starting.Task;

    public Task<MatchResult> Finished => _finished.Task;

    public void ApplySnapshot(DuelSnapshot snapshot)
    {
        lock (_gate)
        {
            Replica.ApplySnapshot(snapshot);
        }
    }

    // Readers run under the same gate as the receive loop, so a caller never sees a board that is
    // half way through a tick. Whatever runs here has to stay short for the same reason.
    public TResult Read<TState, TResult>(TState state, Func<TState, DuelReplica, TResult> read)
    {
        lock (_gate)
        {
            return read(state, Replica);
        }
    }

    public void OnMatchStarting(MatchStartInfo info)
    {
        lock (_gate)
        {
            Replica.ApplyMatchStart(info);
        }

        _starting.TrySetResult(info);
    }

    public void OnTickDelta(TickDelta delta)
    {
        lock (_gate)
        {
            Replica.ApplyTickDelta(delta);
        }
    }

    public void OnMatchFinished(MatchResult result)
    {
        lock (_gate)
        {
            Replica.ApplyMatchFinished(result);
        }

        _finished.TrySetResult(result);
    }
}
