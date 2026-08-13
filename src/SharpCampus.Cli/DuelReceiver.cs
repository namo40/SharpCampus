using SharpCampus.Cli.Duel;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Values;

namespace SharpCampus.Cli;

// Everything a duel room pushes arrives on the hub's receive loop, while the command thread applies
// the snapshot it asked for and the render loop reads the boards; the lock is what keeps all three
// off each other's replica.
internal sealed class DuelReceiver(DuelStatus status) : IDuelHubReceiver
{
    private readonly Lock _gate = new();

    // A room runs more than one game, so each of these is replaced rather than awaited twice. They are
    // read from the command thread and completed from the receive loop, hence the volatile access.
    private TaskCompletionSource<MatchStartInfo> _starting = Armed<MatchStartInfo>();
    private TaskCompletionSource<MatchResult> _finished = Armed<MatchResult>();
    private TaskCompletionSource<RematchOffer> _offered = Armed<RematchOffer>();
    private TaskCompletionSource<bool> _declined = Armed<bool>();

    public DuelReplica Replica { get; } = new();

    public Task<MatchStartInfo> Starting => Volatile.Read(ref _starting).Task;

    public Task<MatchResult> Finished => Volatile.Read(ref _finished).Task;

    public Task<RematchOffer> Offered => Volatile.Read(ref _offered).Task;

    public Task<bool> Declined => Volatile.Read(ref _declined).Task;

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

        // A game that is under way settles the previous one's offer, whichever way it went.
        Volatile.Write(ref _offered, Armed<RematchOffer>());
        Volatile.Write(ref _declined, Armed<bool>());
        Volatile.Read(ref _starting).TrySetResult(info);
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

        Volatile.Read(ref _finished).TrySetResult(result);
    }

    public void OnPresenceChanged(PlayerIndex player, bool connected)
        => status.SetConnected(player.AsPrimitive(), connected);

    // The prompt belongs to the command flow: the receive loop must not sit on a keyboard, and it is
    // still free to take messages while this call is outstanding. An answer that never comes is a
    // real case too, and it is what leaves the offer to the room's own timeout.
    public Task<bool> AskRematchAsync(int timeoutSeconds, CancellationToken cancellationToken)
    {
        var offer = new RematchOffer(this, timeoutSeconds);
        Volatile.Read(ref _offered).TrySetResult(offer);

        return offer.Answer;
    }

    public void OnRematchDeclined() => Volatile.Read(ref _declined).TrySetResult(true);

    // Re-arming before an accepting answer travels is what makes the next game safe: the room only
    // announces it once both answers are in, so nothing can arrive against the state just replaced.
    internal void Rearm()
    {
        Volatile.Write(ref _starting, Armed<MatchStartInfo>());
        Volatile.Write(ref _finished, Armed<MatchResult>());
    }

    private static TaskCompletionSource<T> Armed<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

// The question the room is waiting on, handed to whoever owns the console.
internal sealed class RematchOffer(DuelReceiver receiver, int timeoutSeconds)
{
    private readonly TaskCompletionSource<bool> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int TimeoutSeconds { get; } = timeoutSeconds;

    internal Task<bool> Answer => _answer.Task;

    public void Accept()
    {
        receiver.Rearm();
        _answer.TrySetResult(true);
    }

    public void Decline() => _answer.TrySetResult(false);
}
