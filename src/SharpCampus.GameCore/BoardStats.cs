namespace SharpCampus.GameCore;

// What one board did over one game. Accumulated as it happens rather than replayed from the tick
// events: a hard drop leaves no event of its own, because a lock does not say what caused it.
public readonly record struct BoardStats(
    int LinesCleared,
    int Quads,
    int GarbageSent,
    int HardDrops,
    int MaxCombo);
