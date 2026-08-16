namespace SharpCampus.GameCore;

// Every pose a piece can be dropped into from above the stack, and what each one leaves behind. Only
// straight vertical drops are enumerated because that is exactly what rotate, then move, then hard
// drop can reach: a candidate here is always one an input sequence can actually produce.
public static class Placements
{
    private const int Width = BoardSimulation.Width;
    private const int Height = BoardSimulation.Height;
    private const int RotationCount = 4;

    // Symmetric rotations of O, I, S and Z repeat poses the previous rotation already covers. The
    // duplicates are left in: they score identically, so picking either is the same placement.
    public static List<Placement> Enumerate(ReadOnlySpan<CellKind> cells, PieceKind kind)
    {
        var placements = new List<Placement>();
        Span<CellKind> board = stackalloc CellKind[Width * Height];

        for (var index = 0; index < RotationCount; index++)
        {
            var rotation = (Rotation)index;
            var shape = Tetrominoes.GetCells(kind, rotation);
            Bounds(shape, out var minX, out var maxX, out var maxY);

            for (var x = -minX; x + maxX < Width; x++)
            {
                if (!TryDrop(cells, shape, x, Height - 1 - maxY, out var y))
                {
                    continue;
                }

                cells.CopyTo(board);
                Imprint(board, shape, kind, x, y);
                placements.Add(new Placement(rotation, x, y, ClearFullRows(board), Evaluate(board)));
            }
        }

        return placements;
    }

    public static BoardMetrics Evaluate(ReadOnlySpan<CellKind> cells)
    {
        var aggregateHeight = 0;
        var holes = 0;
        var bumpiness = 0;
        var previousHeight = 0;

        for (var x = 0; x < Width; x++)
        {
            var height = HeightOf(cells, x);

            for (var y = 0; y < height - 1; y++)
            {
                if (cells[(y * Width) + x] == CellKind.Empty)
                {
                    holes++;
                }
            }

            aggregateHeight += height;
            if (x > 0)
            {
                bumpiness += Math.Abs(height - previousHeight);
            }

            previousHeight = height;
        }

        return new BoardMetrics(aggregateHeight, holes, bumpiness);
    }

    private static int HeightOf(ReadOnlySpan<CellKind> cells, int x)
    {
        for (var y = Height - 1; y >= 0; y--)
        {
            if (cells[(y * Width) + x] != CellKind.Empty)
            {
                return y + 1;
            }
        }

        return 0;
    }

    private static void Bounds(ReadOnlySpan<PieceCell> shape, out int minX, out int maxX, out int maxY)
    {
        minX = shape[0].X;
        maxX = shape[0].X;
        maxY = shape[0].Y;

        for (var i = 1; i < shape.Length; i++)
        {
            minX = Math.Min(minX, shape[i].X);
            maxX = Math.Max(maxX, shape[i].X);
            maxY = Math.Max(maxY, shape[i].Y);
        }
    }

    // Starts as high as the board allows and falls until the next row down is blocked. A column whose
    // stack already reaches the ceiling has nowhere to enter from, and yields no candidate.
    private static bool TryDrop(ReadOnlySpan<CellKind> cells, ReadOnlySpan<PieceCell> shape, int x, int topY, out int y)
    {
        y = topY;
        if (Collides(cells, shape, x, y))
        {
            return false;
        }

        while (!Collides(cells, shape, x, y - 1))
        {
            y--;
        }

        return true;
    }

    private static bool Collides(ReadOnlySpan<CellKind> cells, ReadOnlySpan<PieceCell> shape, int x, int y)
    {
        for (var i = 0; i < shape.Length; i++)
        {
            var cellX = x + shape[i].X;
            var cellY = y + shape[i].Y;

            if (cellX < 0
                || cellX >= Width
                || cellY < 0
                || cellY >= Height
                || cells[(cellY * Width) + cellX] != CellKind.Empty)
            {
                return true;
            }
        }

        return false;
    }

    private static void Imprint(Span<CellKind> cells, ReadOnlySpan<PieceCell> shape, PieceKind kind, int x, int y)
    {
        for (var i = 0; i < shape.Length; i++)
        {
            cells[((y + shape[i].Y) * Width) + x + shape[i].X] = Tetrominoes.ToCell(kind);
        }
    }

    private static int ClearFullRows(Span<CellKind> cells)
    {
        var write = 0;
        var cleared = 0;

        for (var y = 0; y < Height; y++)
        {
            if (IsRowFull(cells, y))
            {
                cleared++;
                continue;
            }

            if (write != y)
            {
                cells.Slice(y * Width, Width).CopyTo(cells.Slice(write * Width, Width));
            }

            write++;
        }

        for (var y = write; y < Height; y++)
        {
            cells.Slice(y * Width, Width).Clear();
        }

        return cleared;
    }

    private static bool IsRowFull(ReadOnlySpan<CellKind> cells, int y)
    {
        for (var x = 0; x < Width; x++)
        {
            if (cells[(y * Width) + x] == CellKind.Empty)
            {
                return false;
            }
        }

        return true;
    }
}
