namespace SharpCampus.GameCore;

// Piece shapes live in a square rotation box whose origin is its bottom-left corner, in the same
// bottom-up coordinate system as the board. A piece position is that box origin, so parts of
// the box may sit outside the board as long as no filled cell does.
public static class Tetrominoes
{
    public const int KindCount = 7;

    private const int RotationCount = 4;

    private static readonly int[] _boxSizes = [4, 2, 3, 3, 3, 3, 3];
    private static readonly int[] _spawnX = [3, 4, 3, 3, 3, 3, 3];
    private static readonly int[] _spawnY = [18, 20, 19, 19, 19, 19, 19];

    private static readonly PieceCell[][] _shapes = BuildShapes();
    private static readonly PieceCell[][] _jlstzKicks = BuildJlstzKicks();
    private static readonly PieceCell[][] _iKicks = BuildIKicks();

    public static ReadOnlySpan<PieceCell> GetCells(PieceKind kind, Rotation rotation)
        => _shapes[(IndexOf(kind) * RotationCount) + (int)rotation];

    public static int GetBoxSize(PieceKind kind) => _boxSizes[IndexOf(kind)];

    public static int GetSpawnX(PieceKind kind) => _spawnX[IndexOf(kind)];

    public static int GetSpawnY(PieceKind kind) => _spawnY[IndexOf(kind)];

    public static CellKind ToCell(PieceKind kind) => (CellKind)kind;

    public static int IndexOf(PieceKind kind) => (int)kind - 1;

    internal static ReadOnlySpan<PieceCell> GetKicks(PieceKind kind, Rotation from, Rotation to)
        => (kind == PieceKind.I ? _iKicks : _jlstzKicks)[KickIndex(from, to)];

    private static int KickIndex(Rotation from, Rotation to) => ((int)from * RotationCount) + (int)to;

    private static PieceCell[][] BuildShapes()
    {
        // Spawn states, drawn top row first: the flat side faces down and the whole piece sits
        // in the two hidden rows above the visible field.
        var spawnStates = new PieceCell[][]
        {
            [new(0, 2), new(1, 2), new(2, 2), new(3, 2)],
            [new(0, 0), new(1, 0), new(0, 1), new(1, 1)],
            [new(1, 2), new(0, 1), new(1, 1), new(2, 1)],
            [new(1, 2), new(2, 2), new(0, 1), new(1, 1)],
            [new(0, 2), new(1, 2), new(1, 1), new(2, 1)],
            [new(0, 2), new(0, 1), new(1, 1), new(2, 1)],
            [new(2, 2), new(0, 1), new(1, 1), new(2, 1)],
        };

        var shapes = new PieceCell[KindCount * RotationCount][];
        for (var kind = 0; kind < KindCount; kind++)
        {
            var cells = spawnStates[kind];
            for (var rotation = 0; rotation < RotationCount; rotation++)
            {
                shapes[(kind * RotationCount) + rotation] = cells;
                cells = RotateCw(cells, _boxSizes[kind]);
            }
        }

        return shapes;
    }

    private static PieceCell[] RotateCw(PieceCell[] cells, int boxSize)
    {
        var rotated = new PieceCell[cells.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            rotated[i] = new PieceCell(cells[i].Y, boxSize - 1 - cells[i].X);
        }

        return rotated;
    }

    // Standard SRS (Super Rotation System) wall kick data, expressed with y growing upward.
    // Source: https://harddrop.com/wiki/SRS
    // The first offset that produces a collision-free placement wins; if none does, the
    // rotation is rejected.
    private static PieceCell[][] BuildJlstzKicks()
    {
        var kicks = EmptyKickTable();
        kicks[KickIndex(Rotation.Spawn, Rotation.Right)] = [new(0, 0), new(-1, 0), new(-1, 1), new(0, -2), new(-1, -2)];
        kicks[KickIndex(Rotation.Right, Rotation.Spawn)] = [new(0, 0), new(1, 0), new(1, -1), new(0, 2), new(1, 2)];
        kicks[KickIndex(Rotation.Right, Rotation.Half)] = [new(0, 0), new(1, 0), new(1, -1), new(0, 2), new(1, 2)];
        kicks[KickIndex(Rotation.Half, Rotation.Right)] = [new(0, 0), new(-1, 0), new(-1, 1), new(0, -2), new(-1, -2)];
        kicks[KickIndex(Rotation.Half, Rotation.Left)] = [new(0, 0), new(1, 0), new(1, 1), new(0, -2), new(1, -2)];
        kicks[KickIndex(Rotation.Left, Rotation.Half)] = [new(0, 0), new(-1, 0), new(-1, -1), new(0, 2), new(-1, 2)];
        kicks[KickIndex(Rotation.Left, Rotation.Spawn)] = [new(0, 0), new(-1, 0), new(-1, -1), new(0, 2), new(-1, 2)];
        kicks[KickIndex(Rotation.Spawn, Rotation.Left)] = [new(0, 0), new(1, 0), new(1, 1), new(0, -2), new(1, -2)];
        return kicks;
    }

    private static PieceCell[][] BuildIKicks()
    {
        var kicks = EmptyKickTable();
        kicks[KickIndex(Rotation.Spawn, Rotation.Right)] = [new(0, 0), new(-2, 0), new(1, 0), new(-2, -1), new(1, 2)];
        kicks[KickIndex(Rotation.Right, Rotation.Spawn)] = [new(0, 0), new(2, 0), new(-1, 0), new(2, 1), new(-1, -2)];
        kicks[KickIndex(Rotation.Right, Rotation.Half)] = [new(0, 0), new(-1, 0), new(2, 0), new(-1, 2), new(2, -1)];
        kicks[KickIndex(Rotation.Half, Rotation.Right)] = [new(0, 0), new(1, 0), new(-2, 0), new(1, -2), new(-2, 1)];
        kicks[KickIndex(Rotation.Half, Rotation.Left)] = [new(0, 0), new(2, 0), new(-1, 0), new(2, 1), new(-1, -2)];
        kicks[KickIndex(Rotation.Left, Rotation.Half)] = [new(0, 0), new(-2, 0), new(1, 0), new(-2, -1), new(1, 2)];
        kicks[KickIndex(Rotation.Left, Rotation.Spawn)] = [new(0, 0), new(1, 0), new(-2, 0), new(1, -2), new(-2, 1)];
        kicks[KickIndex(Rotation.Spawn, Rotation.Left)] = [new(0, 0), new(-1, 0), new(2, 0), new(-1, 2), new(2, -1)];
        return kicks;
    }

    private static PieceCell[][] EmptyKickTable()
    {
        var kicks = new PieceCell[RotationCount * RotationCount][];
        for (var i = 0; i < kicks.Length; i++)
        {
            kicks[i] = [];
        }

        return kicks;
    }
}
