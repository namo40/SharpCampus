namespace SharpCampus.GameCore;

// Read-only projection of one board, for rendering, snapshots and verification.
// Cells are addressed x: 0 (left) .. 9 (right), y: 0 (bottom) .. 21 (top); rows 20 and 21 are the
// hidden spawn buffer above the 20 visible rows. The active piece is NOT imprinted into the cells.
public sealed class BoardView
{
    private readonly BoardSimulation _board;

    internal BoardView(BoardSimulation board) => _board = board;

    public int PlayerIndex => _board.PlayerIndex;

    // Row-major, index = (y * Width) + x.
    public ReadOnlySpan<CellKind> Cells => _board.Cells;

    public ActivePieceView? ActivePiece => _board.ActivePiece;

    public IReadOnlyList<PieceKind> Next => _board.Next;

    public PieceKind? Hold => _board.Hold;

    public bool HoldAvailable => _board.HoldAvailable;

    public bool SoftDropActive => _board.SoftDropActive;

    public int PendingGarbage => _board.PendingGarbage;

    public int Combo => _board.Combo;

    public int Level => _board.Level;

    public int ElapsedTicks => _board.ElapsedTicks;

    public bool ToppedOut => _board.ToppedOut;

    public BoardStats Stats => _board.Stats;

    public CellKind GetCell(int x, int y) => _board.Cells[(y * BoardSimulation.Width) + x];
}
