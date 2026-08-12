using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.Shared.Tests;

internal static class DuelFixture
{
    public static readonly GameInput[] AllInputs =
    [
        GameInput.MoveLeft,
        GameInput.MoveRight,
        GameInput.RotateCw,
        GameInput.RotateCcw,
        GameInput.SoftDropOn,
        GameInput.SoftDropOff,
        GameInput.Hold,
        GameInput.HardDrop,
    ];

    public static SimulationConfig Config => new(
        LockDelayTicks: 10,
        LockResetMax: 15,
        NextCount: 5,
        GarbageCapPerLock: 8,
        GravityTicksPerCell: [20, 18, 15, 12, 10, 8, 7, 6, 5, 4, 3, 3, 2, 2, 1],
        LevelUpIntervalTicks: 600,
        MaxLevel: 15,
        AttackByLines: [0, 1, 2, 4],
        ComboBonus: [new ComboBonusRange(2, 3, 1), new ComboBonusRange(4, 5, 2), new ComboBonusRange(6, 99, 3)]);

    public static GameInput[] RandomInputs(Random random)
    {
        var inputs = new GameInput[random.Next(0, 4)];
        for (var i = 0; i < inputs.Length; i++)
        {
            inputs[i] = AllInputs[random.Next(AllInputs.Length)];
        }

        return inputs;
    }

    public static CellKind[] EmptyCells() => new CellKind[DuelReplicaBoard.Width * DuelReplicaBoard.Height];

    public static CellKind[] FillRow(this CellKind[] cells, int y, params int[] holeColumns)
    {
        for (var x = 0; x < DuelReplicaBoard.Width; x++)
        {
            if (Array.IndexOf(holeColumns, x) < 0)
            {
                cells[(y * DuelReplicaBoard.Width) + x] = CellKind.Garbage;
            }
        }

        return cells;
    }

    public static DuelSnapshot Snapshot(int tick, CellKind[] cells0, CellKind[]? cells1 = null)
        => new(new TickNumber(tick), [PlayerAt(0, cells0), PlayerAt(1, cells1 ?? EmptyCells())]);

    public static TickDelta Delta(int tick, params DuelEvent[] events)
        => new(new TickNumber(tick), [IdlePlayer(0), IdlePlayer(1)], events);

    public static void AssertMatches(BoardView expected, DuelReplicaBoard actual)
    {
        Assert.Equal(expected.Cells.ToArray(), actual.Cells.ToArray());
        Assert.Equal(expected.Next, actual.Next);
        Assert.Equal(expected.Hold, actual.Hold);
        Assert.Equal(expected.HoldAvailable, actual.HoldAvailable);
        Assert.Equal(expected.ActivePiece, actual.ActivePiece);
        Assert.Equal(expected.SoftDropActive, actual.SoftDropActive);
        Assert.Equal(expected.PendingGarbage, actual.PendingGarbage);
        Assert.Equal(expected.Combo, actual.Combo);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.ElapsedTicks, actual.ElapsedTicks);
        Assert.Equal(expected.ToppedOut, actual.ToppedOut);
    }

    private static PlayerSnapshot PlayerAt(int playerIndex, CellKind[] cells)
    {
        var bytes = new byte[cells.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            bytes[i] = (byte)cells[i];
        }

        return new PlayerSnapshot(
            new PlayerIndex(playerIndex),
            bytes,
            [PieceKind.I, PieceKind.O, PieceKind.T, PieceKind.S, PieceKind.Z],
            HasHold: false,
            Hold: default,
            HoldAvailable: true,
            HasActivePiece: false,
            ActiveKind: default,
            ActiveRotation: default,
            ActiveX: 0,
            ActiveY: 0,
            SoftDropActive: false,
            PendingGarbage: 0,
            Combo: 0,
            Level: 1,
            ElapsedTicks: 0,
            ToppedOut: false);
    }

    private static PlayerDelta IdlePlayer(int playerIndex)
        => new(new PlayerIndex(playerIndex), false, default, default, 0, 0, false, 0, 0, 1);
}
