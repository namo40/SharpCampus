using R3;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Values;

namespace SharpCampus.BotServer.Bots;

// Everything a duel room pushes arrives on the hub's receive loop, while the bot task applies the
// snapshot it asked for and the planner reads the boards; the lock is what keeps all three off each
// other's replica.
internal sealed class BotReceiver : IDuelHubReceiver, IDisposable
{
    private readonly Lock _gate = new();
    private readonly Subject<TickDelta> _deltas = new();

    // A bot declines every rematch, so a room only ever announces one game to it and neither of these
    // is ever re-armed.
    private readonly TaskCompletionSource<MatchStartInfo> _starting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<MatchResult> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DuelReplica Replica { get; } = new();

    // Pushed once the replica already reflects the tick, so a subscriber that reads the board sees the
    // tick it was told about rather than the one before it.
    public Observable<TickDelta> Deltas => _deltas;

    public Task<MatchStartInfo> Starting => _starting.Task;

    public Task<MatchResult> Finished => _finished.Task;

    public void ApplySnapshot(DuelSnapshot snapshot)
    {
        lock (_gate)
        {
            Replica.ApplySnapshot(snapshot);
        }
    }

    // Readers run under the same gate as the receive loop, so a caller never sees a board half way
    // through a tick. Whatever runs here has to stay short for the same reason.
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

        _deltas.OnNext(delta);
    }

    public void OnMatchFinished(MatchResult result)
    {
        lock (_gate)
        {
            Replica.ApplyMatchFinished(result);
        }

        _finished.TrySetResult(result);
    }

    public void OnPresenceChanged(PlayerIndex player, bool connected)
    {
    }

    // Answering straight away is what lets the room close instead of sitting out the offer's timeout.
    public Task<bool> AskRematchAsync(int timeoutSeconds, CancellationToken cancellationToken)
        => Task.FromResult(false);

    public void OnRematchDeclined()
    {
    }

    public void Dispose() => _deltas.Dispose();
}
