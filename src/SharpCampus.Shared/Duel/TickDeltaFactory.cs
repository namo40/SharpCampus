using System.Runtime.InteropServices;
using SharpCampus.GameCore;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// Turns simulation output into the messages the room broadcasts. Pure mapping: it reads the
/// simulation's views and never touches them.
/// </summary>
public static class TickDeltaFactory
{
    /// <summary>
    /// Projects one simulated tick onto the wire.
    /// </summary>
    /// <param name="result">What the tick returned.</param>
    /// <param name="board1">Seat 0 as it stands after the tick.</param>
    /// <param name="board2">Seat 1 as it stands after the tick.</param>
    /// <returns>The delta to broadcast for this tick.</returns>
    public static TickDelta Create(DuelTickResult result, BoardView board1, BoardView board2)
    {
        var events = new DuelEvent[result.Events.Count];
        for (var i = 0; i < events.Length; i++)
        {
            events[i] = DuelEvent.From(result.Events[i]);
        }

        return new TickDelta(
            new TickNumber(result.TickNumber),
            [CreatePlayerDelta(board1), CreatePlayerDelta(board2)],
            events);
    }

    /// <summary>
    /// Reads both boards in full.
    /// </summary>
    /// <param name="tickNumber">Tick the boards are being read at.</param>
    /// <param name="board1">Seat 0.</param>
    /// <param name="board2">Seat 1.</param>
    /// <returns>A snapshot a client can rebuild both boards from.</returns>
    public static DuelSnapshot CreateSnapshot(int tickNumber, BoardView board1, BoardView board2)
        => new(new TickNumber(tickNumber), [CreatePlayerSnapshot(board1), CreatePlayerSnapshot(board2)]);

    private static PlayerDelta CreatePlayerDelta(BoardView board)
    {
        var active = board.ActivePiece;
        return new PlayerDelta(
            new PlayerIndex(board.PlayerIndex),
            active.HasValue,
            active?.Kind ?? default,
            active?.Rotation ?? default,
            active?.X ?? 0,
            active?.Y ?? 0,
            board.SoftDropActive,
            board.PendingGarbage,
            board.Combo,
            board.Level);
    }

    private static PlayerSnapshot CreatePlayerSnapshot(BoardView board)
    {
        var next = new PieceKind[board.Next.Count];
        for (var i = 0; i < next.Length; i++)
        {
            next[i] = board.Next[i];
        }

        var active = board.ActivePiece;
        return new PlayerSnapshot(
            new PlayerIndex(board.PlayerIndex),
            MemoryMarshal.Cast<CellKind, byte>(board.Cells).ToArray(),
            next,
            board.Hold.HasValue,
            board.Hold ?? default,
            board.HoldAvailable,
            active.HasValue,
            active?.Kind ?? default,
            active?.Rotation ?? default,
            active?.X ?? 0,
            active?.Y ?? 0,
            board.SoftDropActive,
            board.PendingGarbage,
            board.Combo,
            board.Level,
            board.ElapsedTicks,
            board.ToppedOut);
    }
}
