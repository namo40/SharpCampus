using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// A client-side copy of both boards, driven entirely by what a duel room broadcasts. Applying the
/// same messages the server sent reproduces the server's boards cell for cell.
/// </summary>
// Not thread safe: a caller that applies messages from a receive loop and from its own thread has to
// serialise them itself.
public sealed class DuelReplica
{
    private readonly DuelReplicaBoard[] _boards = [new(0), new(1)];

    /// <summary>Seat 0's board.</summary>
    public DuelReplicaBoard Board1 => _boards[0];

    /// <summary>Seat 1's board.</summary>
    public DuelReplicaBoard Board2 => _boards[1];

    /// <summary>Both boards, in seat order.</summary>
    public IReadOnlyList<DuelReplicaBoard> Boards => _boards;

    /// <summary>Who is playing, once the match has been announced.</summary>
    public MatchStartInfo? MatchStart { get; private set; }

    /// <summary>How the match ended, once it has.</summary>
    public MatchResult? Result { get; private set; }

    /// <summary>Last tick reflected in the boards.</summary>
    public TickNumber Tick { get; private set; }

    /// <summary>Whether a snapshot has been applied. Deltas are ignored until one has.</summary>
    public bool IsReady { get; private set; }

    /// <summary>
    /// Whether a delta arrived out of sequence, which leaves the boards guesswork until a fresh
    /// snapshot is applied.
    /// </summary>
    public bool HasMissedTicks { get; private set; }

    /// <summary>Records the announced match and clears everything the previous one left behind.</summary>
    /// <param name="info">Seats and countdown as broadcast.</param>
    // A room announces every game it runs, so this is also what makes a replica reusable across a
    // rematch: the boards go back to empty and stay there until the next snapshot arrives.
    public void ApplyMatchStart(MatchStartInfo info)
    {
        MatchStart = info;
        Result = null;
        Tick = default;
        IsReady = false;
        HasMissedTicks = false;

        foreach (var board in _boards)
        {
            board.Clear();
        }
    }

    /// <summary>Replaces both boards with a full read of the server's state.</summary>
    /// <param name="snapshot">Boards as of some tick.</param>
    public void ApplySnapshot(DuelSnapshot snapshot)
    {
        foreach (var player in snapshot.Players)
        {
            _boards[player.PlayerIndex.AsPrimitive()].Load(player);
        }

        Tick = snapshot.Tick;
        IsReady = true;
        HasMissedTicks = false;
    }

    /// <summary>
    /// Advances both boards by one tick.
    /// </summary>
    /// <param name="delta">What that tick changed.</param>
    /// <returns>Whether the delta was applied; a delta the snapshot already covers is not.</returns>
    public bool ApplyTickDelta(TickDelta delta)
    {
        if (!IsReady || delta.Tick <= Tick)
        {
            return false;
        }

        HasMissedTicks |= delta.Tick.AsPrimitive() != Tick.AsPrimitive() + 1;

        foreach (var board in _boards)
        {
            board.BeginTick();
        }

        // Order matters: the events describe the tick as it happened, and the per-player state is
        // what the board looked like once it was over.
        foreach (var duelEvent in delta.Events)
        {
            _boards[duelEvent.PlayerIndex.AsPrimitive()].Apply(duelEvent.ToTickEvent());
        }

        foreach (var player in delta.Players)
        {
            _boards[player.PlayerIndex.AsPrimitive()].Apply(player);
        }

        Tick = delta.Tick;
        return true;
    }

    /// <summary>Records the end of the match.</summary>
    /// <param name="result">Winner and reason as broadcast.</param>
    public void ApplyMatchFinished(MatchResult result) => Result = result;
}
