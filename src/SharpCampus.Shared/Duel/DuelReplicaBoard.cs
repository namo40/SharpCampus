using SharpCampus.GameCore;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// One board as a client believes it to be, rebuilt from snapshots and tick deltas. Mirrors the
/// read surface of the server's own board view so rendering code does not care which side it runs on.
/// </summary>
public sealed class DuelReplicaBoard
{
    /// <summary>Board width in cells.</summary>
    public const int Width = BoardSimulation.Width;

    /// <summary>Board height in cells, including the hidden spawn rows.</summary>
    public const int Height = BoardSimulation.Height;

    private readonly CellKind[] _cells = new CellKind[Width * Height];
    private readonly List<PieceKind> _next = [];

    // A hold that finds the hold slot empty spawns a piece, and the spawn is what re-arms the swap;
    // the simulation spends the allowance only after that spawn, so the replica defers it too.
    private bool _holdSpendPending;

    internal DuelReplicaBoard(int playerIndex) => PlayerIndex = playerIndex;

    /// <summary>Seat this board belongs to.</summary>
    public int PlayerIndex { get; }

    /// <summary>Settled cells, row-major, index = (y * <see cref="Width"/>) + x. The falling piece is not imprinted.</summary>
    public ReadOnlySpan<CellKind> Cells => _cells;

    /// <summary>Upcoming pieces, soonest first.</summary>
    public IReadOnlyList<PieceKind> Next => _next;

    /// <summary>Held piece, or null when the hold slot is empty.</summary>
    public PieceKind? Hold { get; private set; }

    /// <summary>Whether the falling piece may still be swapped into hold.</summary>
    public bool HoldAvailable { get; private set; }

    /// <summary>Falling piece, or null between a lock and the next spawn.</summary>
    public ActivePieceView? ActivePiece { get; private set; }

    /// <summary>Whether the player is holding soft drop.</summary>
    public bool SoftDropActive { get; private set; }

    /// <summary>Garbage rows queued against this board.</summary>
    public int PendingGarbage { get; private set; }

    /// <summary>Consecutive locks that cleared lines.</summary>
    public int Combo { get; private set; }

    /// <summary>Gravity level the board has reached.</summary>
    public int Level { get; private set; }

    /// <summary>Ticks this board has simulated.</summary>
    public int ElapsedTicks { get; private set; }

    /// <summary>Whether this board has lost.</summary>
    public bool ToppedOut { get; private set; }

    /// <summary>Reads one cell.</summary>
    /// <param name="x">Column, 0 at the left.</param>
    /// <param name="y">Row, 0 at the bottom.</param>
    /// <returns>What occupies the cell.</returns>
    public CellKind GetCell(int x, int y) => _cells[(y * Width) + x];

    internal void Clear()
    {
        Array.Clear(_cells, 0, _cells.Length);
        _next.Clear();
        _holdSpendPending = false;

        Hold = null;
        HoldAvailable = false;
        ActivePiece = null;
        SoftDropActive = false;
        PendingGarbage = 0;
        Combo = 0;
        Level = 0;
        ElapsedTicks = 0;
        ToppedOut = false;
    }

    internal void Load(PlayerSnapshot snapshot)
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            _cells[i] = (CellKind)snapshot.Cells[i];
        }

        _next.Clear();
        _next.AddRange(snapshot.Next);
        _holdSpendPending = false;

        Hold = snapshot.HasHold ? snapshot.Hold : null;
        HoldAvailable = snapshot.HoldAvailable;
        ActivePiece = snapshot.HasActivePiece
            ? new ActivePieceView(snapshot.ActiveKind, snapshot.ActiveRotation, snapshot.ActiveX, snapshot.ActiveY)
            : null;
        SoftDropActive = snapshot.SoftDropActive;
        PendingGarbage = snapshot.PendingGarbage;
        Combo = snapshot.Combo;
        Level = snapshot.Level;
        ElapsedTicks = snapshot.ElapsedTicks;
        ToppedOut = snapshot.ToppedOut;
    }

    internal void BeginTick()
    {
        if (!ToppedOut)
        {
            ElapsedTicks++;
        }
    }

    internal void Apply(TickEvent tickEvent)
    {
        switch (tickEvent.Kind)
        {
            case TickEventKind.PieceSpawned:
                Spawn((PieceKind)tickEvent.Extra);
                break;
            case TickEventKind.PieceLocked:
                Imprint(tickEvent.LockedKind, tickEvent.LockedRotation, tickEvent.LockedX, tickEvent.LockedY);
                ClearFullRows();
                break;
            case TickEventKind.GarbageApplied:
                InsertGarbageRows(tickEvent.Value, tickEvent.Extra);
                break;
            case TickEventKind.HoldSwapped:
                SwapHold((PieceKind)tickEvent.Value);
                break;
            case TickEventKind.ToppedOut:
                ToppedOut = true;
                break;
        }
    }

    internal void Apply(PlayerDelta delta)
    {
        ActivePiece = delta.HasActivePiece
            ? new ActivePieceView(delta.ActiveKind, delta.ActiveRotation, delta.ActiveX, delta.ActiveY)
            : null;
        SoftDropActive = delta.SoftDropActive;
        PendingGarbage = delta.PendingGarbage;
        Combo = delta.Combo;
        Level = delta.Level;
    }

    private void Spawn(PieceKind appendedToNext)
    {
        if (_next.Count > 0)
        {
            _next.RemoveAt(0);
        }

        _next.Add(appendedToNext);
        HoldAvailable = !_holdSpendPending;
        _holdSpendPending = false;
    }

    private void SwapHold(PieceKind movedToHold)
    {
        var spawns = Hold is null;
        Hold = movedToHold;

        if (spawns)
        {
            _holdSpendPending = true;
        }
        else
        {
            HoldAvailable = false;
        }
    }

    private void Imprint(PieceKind kind, Rotation rotation, int x, int y)
    {
        var cells = Tetrominoes.GetCells(kind, rotation);
        for (var i = 0; i < cells.Length; i++)
        {
            _cells[((y + cells[i].Y) * Width) + x + cells[i].X] = Tetrominoes.ToCell(kind);
        }
    }

    // Which rows a lock cleared is never transmitted: after the pose is imprinted the replica can see
    // them, and recomputing keeps the delta free of a list that is redundant by construction.
    private void ClearFullRows()
    {
        var write = 0;
        for (var y = 0; y < Height; y++)
        {
            if (IsRowFull(y))
            {
                continue;
            }

            if (write != y)
            {
                Array.Copy(_cells, y * Width, _cells, write * Width, Width);
            }

            write++;
        }

        for (var y = write; y < Height; y++)
        {
            Array.Clear(_cells, y * Width, Width);
        }
    }

    private void InsertGarbageRows(int rows, int holeColumn)
    {
        for (var y = Height - 1; y >= rows; y--)
        {
            Array.Copy(_cells, (y - rows) * Width, _cells, y * Width, Width);
        }

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                _cells[(y * Width) + x] = x == holeColumn ? CellKind.Empty : CellKind.Garbage;
            }
        }
    }

    private bool IsRowFull(int y)
    {
        for (var x = 0; x < Width; x++)
        {
            if (_cells[(y * Width) + x] == CellKind.Empty)
            {
                return false;
            }
        }

        return true;
    }
}
