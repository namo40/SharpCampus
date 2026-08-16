namespace SharpCampus.GameCore;

// One resting pose a piece can be dropped into, with what the board looks like once it has locked and
// any full rows are gone. X and Y are the rotation box origin, the same convention as Tetrominoes.
public readonly record struct Placement(Rotation Rotation, int X, int Y, int LinesCleared, BoardMetrics After);
