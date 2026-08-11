namespace SharpCampus.GameCore;

public readonly record struct ActivePieceView(PieceKind Kind, Rotation Rotation, int X, int Y)
{
    public ReadOnlySpan<PieceCell> Cells => Tetrominoes.GetCells(Kind, Rotation);
}
