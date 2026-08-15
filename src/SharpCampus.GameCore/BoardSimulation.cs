using SharpCampus.GameCore.Internal;

namespace SharpCampus.GameCore;

// One player's board. Everything is measured in ticks; the simulation never observes wall-clock
// time, so a replay of the same inputs always produces the same result.
public sealed class BoardSimulation
{
    public const int Width = 10;
    public const int Height = 22;
    public const int VisibleHeight = 20;

    // Hole columns must not perturb the shared piece stream, so they come from their own
    // generator seeded from the match seed.
    private const ulong GarbageStreamSalt = 0xD1B54A32D192ED03UL;

    private const int QuadLines = 4;

    private readonly SimulationConfig _config;
    private readonly IPieceSource _pieces;
    private readonly int _playerIndex;
    private readonly CellKind[] _cells = new CellKind[Width * Height];
    private readonly List<PieceKind> _next = [];
    private readonly List<GarbageChunk> _garbage = [];
    private readonly List<TickEvent> _ownEvents = [];

    private SplitMix64 _garbageRandom;
    private PieceKind _activeKind;
    private Rotation _activeRotation;
    private int _activeX;
    private int _activeY;
    private bool _hasActive;
    private PieceKind? _hold;
    private bool _holdUsed;
    private bool _softDrop;
    private int _gravityCounter;
    private int _lockTimer;
    private int _lockResets;
    private int _combo;
    private int _elapsedTicks;
    private int _attackThisTick;
    private bool _lockedThisTick;
    private int _linesCleared;
    private int _quads;
    private int _garbageSent;
    private int _hardDrops;
    private int _maxCombo;

    public BoardSimulation(SimulationConfig config, ulong seed, int playerIndex = 0)
        : this(config, new SevenBagPieceSource(seed), seed, playerIndex)
    {
    }

    internal BoardSimulation(SimulationConfig config, IPieceSource pieces, ulong seed, int playerIndex)
    {
        _config = config;
        _pieces = pieces;
        _playerIndex = playerIndex;
        _garbageRandom = new SplitMix64(SplitMix64.Mix(seed ^ GarbageStreamSalt));
        View = new BoardView(this);

        for (var i = 0; i < config.NextCount; i++)
        {
            _next.Add(_pieces.Next());
        }
    }

    public BoardView View { get; }

    public bool ToppedOut { get; private set; }

    public BoardStats Stats => new(_linesCleared, _quads, _garbageSent, _hardDrops, _maxCombo);

    public IReadOnlyList<TickEvent> LastTickEvents => _ownEvents;

    internal bool ReceivedGarbageThisTick { get; private set; }

    internal int PlayerIndex => _playerIndex;

    internal ReadOnlySpan<CellKind> Cells => _cells;

    internal IReadOnlyList<PieceKind> Next => _next;

    internal PieceKind? Hold => _hold;

    internal bool HoldAvailable => !_holdUsed;

    internal bool SoftDropActive => _softDrop;

    internal int Combo => _combo;

    internal int ElapsedTicks => _elapsedTicks;

    internal int Level => _config.LevelAt(_elapsedTicks);

    internal ActivePieceView? ActivePiece
        => _hasActive ? new ActivePieceView(_activeKind, _activeRotation, _activeX, _activeY) : null;

    internal int PendingGarbage
    {
        get
        {
            var total = 0;
            for (var i = 0; i < _garbage.Count; i++)
            {
                total += _garbage[i].Rows;
            }

            return total;
        }
    }

    public int Tick(ReadOnlySpan<GameInput> inputs)
    {
        _ownEvents.Clear();
        return Tick(inputs, _ownEvents);
    }

    // Returns the garbage rows this board attacks with this tick; routing them is the caller's job.
    public int Tick(ReadOnlySpan<GameInput> inputs, List<TickEvent> events)
    {
        _attackThisTick = 0;
        _lockedThisTick = false;
        ReceivedGarbageThisTick = false;

        if (ToppedOut)
        {
            return 0;
        }

        _elapsedTicks++;

        if (!_hasActive)
        {
            Spawn(events);
        }

        for (var i = 0; i < inputs.Length && !ToppedOut && !_lockedThisTick; i++)
        {
            ApplyInput(inputs[i], events);
        }

        if (!ToppedOut && !_lockedThisTick)
        {
            ApplyGravity(events);
        }

        return _attackThisTick;
    }

    public void ReceiveGarbage(int rows)
    {
        if (rows <= 0)
        {
            return;
        }

        _garbage.Add(new GarbageChunk(rows, _garbageRandom.NextInt(Width)));
    }

    internal void LoadCells(ReadOnlySpan<CellKind> cells) => cells.CopyTo(_cells);

    private void ApplyInput(GameInput input, List<TickEvent> events)
    {
        switch (input)
        {
            case GameInput.MoveLeft:
                OnPlayerMove(TryMove(-1, 0));
                break;
            case GameInput.MoveRight:
                OnPlayerMove(TryMove(1, 0));
                break;
            case GameInput.RotateCw:
                OnPlayerMove(TryRotate(1));
                break;
            case GameInput.RotateCcw:
                OnPlayerMove(TryRotate(-1));
                break;
            case GameInput.SoftDropOn:
                _softDrop = true;
                break;
            case GameInput.SoftDropOff:
                _softDrop = false;
                break;
            case GameInput.HardDrop:
                HardDrop(events);
                break;
            case GameInput.Hold:
                TryHold(events);
                break;
        }
    }

    private void OnPlayerMove(bool succeeded)
    {
        if (!succeeded || CanFall() || _lockResets >= _config.LockResetMax)
        {
            return;
        }

        _lockResets++;
        _lockTimer = 0;
    }

    private void ApplyGravity(List<TickEvent> events)
    {
        var interval = _softDrop ? 1 : _config.GravityIntervalTicks(Level);
        _gravityCounter++;
        if (_gravityCounter >= interval)
        {
            _gravityCounter = 0;
            TryMove(0, -1);
        }

        if (CanFall())
        {
            _lockTimer = 0;
            return;
        }

        // Once the reset allowance is spent, grounding locks the piece with no further delay.
        if (_lockResets >= _config.LockResetMax)
        {
            Lock(events);
            return;
        }

        _lockTimer++;
        if (_lockTimer >= _config.LockDelayTicks)
        {
            Lock(events);
        }
    }

    private void HardDrop(List<TickEvent> events)
    {
        _hardDrops++;

        while (TryMove(0, -1))
        {
        }

        Lock(events);
    }

    private void TryHold(List<TickEvent> events)
    {
        if (_holdUsed || !_hasActive)
        {
            return;
        }

        var moved = _activeKind;
        if (_hold is { } previous)
        {
            _hold = moved;
            PlacePiece(previous);
            events.Add(TickEvent.HoldSwapped(_playerIndex, moved, previous));
            if (Collides(previous, Rotation.Spawn, _activeX, _activeY))
            {
                TopOut(events);
            }
        }
        else
        {
            _hold = moved;
            events.Add(TickEvent.HoldSwapped(_playerIndex, moved, _next[0]));
            Spawn(events);
        }

        _holdUsed = true;
    }

    private bool TryMove(int dx, int dy)
    {
        if (!_hasActive || Collides(_activeKind, _activeRotation, _activeX + dx, _activeY + dy))
        {
            return false;
        }

        _activeX += dx;
        _activeY += dy;
        return true;
    }

    private bool TryRotate(int direction)
    {
        if (!_hasActive)
        {
            return false;
        }

        // O occupies the same cells in every state, so rotating it succeeds without moving anything.
        if (_activeKind == PieceKind.O)
        {
            return true;
        }

        var from = _activeRotation;
        var to = (Rotation)(((int)from + (direction > 0 ? 1 : 3)) % 4);
        var kicks = Tetrominoes.GetKicks(_activeKind, from, to);
        for (var i = 0; i < kicks.Length; i++)
        {
            var x = _activeX + kicks[i].X;
            var y = _activeY + kicks[i].Y;
            if (!Collides(_activeKind, to, x, y))
            {
                _activeRotation = to;
                _activeX = x;
                _activeY = y;
                return true;
            }
        }

        return false;
    }

    private bool CanFall() => _hasActive && !Collides(_activeKind, _activeRotation, _activeX, _activeY - 1);

    private bool Collides(PieceKind kind, Rotation rotation, int originX, int originY)
    {
        var cells = Tetrominoes.GetCells(kind, rotation);
        for (var i = 0; i < cells.Length; i++)
        {
            var x = originX + cells[i].X;
            var y = originY + cells[i].Y;
            if (x < 0 || x >= Width || y < 0 || y >= Height || _cells[(y * Width) + x] != CellKind.Empty)
            {
                return true;
            }
        }

        return false;
    }

    private void Lock(List<TickEvent> events)
    {
        _lockedThisTick = true;
        _hasActive = false;

        var cells = Tetrominoes.GetCells(_activeKind, _activeRotation);
        for (var i = 0; i < cells.Length; i++)
        {
            _cells[((_activeY + cells[i].Y) * Width) + _activeX + cells[i].X] = Tetrominoes.ToCell(_activeKind);
        }

        events.Add(TickEvent.PieceLocked(_playerIndex, _activeKind, _activeRotation, _activeX, _activeY));

        var lines = ClearLines();
        if (lines > 0)
        {
            _combo++;
            _linesCleared += lines;
            _maxCombo = Math.Max(_maxCombo, _combo);
            if (lines == QuadLines)
            {
                _quads++;
            }

            events.Add(TickEvent.LinesCleared(_playerIndex, lines, _combo));

            // Outgoing attack cancels the board's own queue before any of it reaches the opponent.
            var attack = _config.AttackFor(lines, _combo);
            attack -= CancelPendingGarbage(attack);
            if (attack > 0)
            {
                _attackThisTick += attack;
                _garbageSent += attack;
                events.Add(TickEvent.GarbageSent(_playerIndex, attack));
            }
        }
        else
        {
            _combo = 0;
            ApplyPendingGarbage(events);
        }

        if (!ToppedOut)
        {
            Spawn(events);
        }
    }

    private int ClearLines()
    {
        var write = 0;
        var cleared = 0;
        for (var y = 0; y < Height; y++)
        {
            if (IsRowFull(y))
            {
                cleared++;
                continue;
            }

            if (write != y)
            {
                CopyRow(y, write);
            }

            write++;
        }

        for (var y = write; y < Height; y++)
        {
            ClearRow(y);
        }

        return cleared;
    }

    private int CancelPendingGarbage(int attack)
    {
        var cancelled = 0;
        while (cancelled < attack && _garbage.Count > 0)
        {
            var chunk = _garbage[0];
            var rows = Math.Min(chunk.Rows, attack - cancelled);
            cancelled += rows;
            if (rows == chunk.Rows)
            {
                _garbage.RemoveAt(0);
            }
            else
            {
                chunk.Rows -= rows;
                _garbage[0] = chunk;
            }
        }

        return cancelled;
    }

    private void ApplyPendingGarbage(List<TickEvent> events)
    {
        var budget = _config.GarbageCapPerLock;
        while (budget > 0 && _garbage.Count > 0)
        {
            var chunk = _garbage[0];
            var rows = Math.Min(chunk.Rows, budget);
            budget -= rows;
            if (rows == chunk.Rows)
            {
                _garbage.RemoveAt(0);
            }
            else
            {
                chunk.Rows -= rows;
                _garbage[0] = chunk;
            }

            ReceivedGarbageThisTick = true;
            events.Add(TickEvent.GarbageApplied(_playerIndex, rows, chunk.HoleColumn));
            InsertGarbageRows(rows, chunk.HoleColumn, events);
            if (ToppedOut)
            {
                return;
            }
        }
    }

    private void InsertGarbageRows(int rows, int holeColumn, List<TickEvent> events)
    {
        var overflow = false;
        for (var y = Height - rows; y < Height && !overflow; y++)
        {
            overflow = !IsRowEmpty(y);
        }

        for (var y = Height - 1; y >= rows; y--)
        {
            CopyRow(y - rows, y);
        }

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                _cells[(y * Width) + x] = x == holeColumn ? CellKind.Empty : CellKind.Garbage;
            }
        }

        if (overflow)
        {
            TopOut(events);
        }
    }

    private void Spawn(List<TickEvent> events)
    {
        var kind = _next[0];
        _next.RemoveAt(0);
        var appended = _pieces.Next();
        _next.Add(appended);

        PlacePiece(kind);
        _holdUsed = false;
        events.Add(TickEvent.PieceSpawned(_playerIndex, kind, appended));

        if (Collides(kind, Rotation.Spawn, _activeX, _activeY))
        {
            TopOut(events);
        }
    }

    private void PlacePiece(PieceKind kind)
    {
        _activeKind = kind;
        _activeRotation = Rotation.Spawn;
        _activeX = Tetrominoes.GetSpawnX(kind);
        _activeY = Tetrominoes.GetSpawnY(kind);
        _hasActive = true;
        _gravityCounter = 0;
        _lockTimer = 0;
        _lockResets = 0;
    }

    private void TopOut(List<TickEvent> events)
    {
        ToppedOut = true;
        events.Add(TickEvent.ToppedOut(_playerIndex));
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

    private bool IsRowEmpty(int y)
    {
        for (var x = 0; x < Width; x++)
        {
            if (_cells[(y * Width) + x] != CellKind.Empty)
            {
                return false;
            }
        }

        return true;
    }

    private void CopyRow(int fromY, int toY) => Array.Copy(_cells, fromY * Width, _cells, toY * Width, Width);

    private void ClearRow(int y) => Array.Clear(_cells, y * Width, Width);
}
