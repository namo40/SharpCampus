namespace SharpCampus.GameCore;

public enum TickEventKind : byte
{
    PieceSpawned = 0,
    PieceLocked = 1,
    LinesCleared = 2,
    GarbageSent = 3,
    GarbageApplied = 4,
    HoldSwapped = 5,
    ToppedOut = 6,
}

// One flat struct for every event kind: the tick loop produces these 20 times a second per room,
// so no interface hierarchy and no boxing. The factory methods name each kind's payload.
public readonly record struct TickEvent(TickEventKind Kind, int PlayerIndex, int Value, int Extra)
{
    public static TickEvent PieceSpawned(int playerIndex, PieceKind spawned, PieceKind appendedToNext)
        => new(TickEventKind.PieceSpawned, playerIndex, (int)spawned, (int)appendedToNext);

    // A viewer that only draws what it is told cannot infer where a piece came to rest: by the time the
    // lock is broadcast the active piece already points at the next spawn. Rotation boxes may hang off
    // the board, so the origin is packed as two signed halves rather than bytes.
    public static TickEvent PieceLocked(int playerIndex, PieceKind kind, Rotation rotation, int x, int y)
        => new(
            TickEventKind.PieceLocked,
            playerIndex,
            (int)kind | ((int)rotation << 8),
            (x & 0xFFFF) | (y << 16));

    public static TickEvent LinesCleared(int playerIndex, int lines, int combo)
        => new(TickEventKind.LinesCleared, playerIndex, lines, combo);

    public static TickEvent GarbageSent(int playerIndex, int rows)
        => new(TickEventKind.GarbageSent, playerIndex, rows, 0);

    public static TickEvent GarbageApplied(int playerIndex, int rows, int holeColumn)
        => new(TickEventKind.GarbageApplied, playerIndex, rows, holeColumn);

    public static TickEvent HoldSwapped(int playerIndex, PieceKind movedToHold, PieceKind nowActive)
        => new(TickEventKind.HoldSwapped, playerIndex, (int)movedToHold, (int)nowActive);

    public static TickEvent ToppedOut(int playerIndex)
        => new(TickEventKind.ToppedOut, playerIndex, 0, 0);

    public PieceKind LockedKind => (PieceKind)(Value & 0xFF);

    public Rotation LockedRotation => (Rotation)((Value >> 8) & 0xFF);

    public int LockedX => (short)(Extra & 0xFFFF);

    public int LockedY => (short)(Extra >> 16);
}
