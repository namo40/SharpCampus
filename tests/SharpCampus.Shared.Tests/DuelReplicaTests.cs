using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.Shared.Tests;

public class DuelReplicaTests
{
    [Fact]
    public void ApplyTickDelta_IsIgnoredUntilASnapshotArrives()
    {
        var replica = new DuelReplica();

        Assert.False(replica.ApplyTickDelta(DuelFixture.Delta(1)));
        Assert.False(replica.IsReady);
    }

    [Fact]
    public void ApplyTickDelta_SkipsTicksTheSnapshotAlreadyCovers()
    {
        var replica = new DuelReplica();
        replica.ApplySnapshot(DuelFixture.Snapshot(5, DuelFixture.EmptyCells()));

        Assert.False(replica.ApplyTickDelta(DuelFixture.Delta(3)));
        Assert.False(replica.ApplyTickDelta(DuelFixture.Delta(5)));
        Assert.True(replica.ApplyTickDelta(DuelFixture.Delta(6)));
        Assert.False(replica.HasMissedTicks);
    }

    [Fact]
    public void ApplyTickDelta_FlagsAGapInTheStream()
    {
        var replica = new DuelReplica();
        replica.ApplySnapshot(DuelFixture.Snapshot(5, DuelFixture.EmptyCells()));

        Assert.True(replica.ApplyTickDelta(DuelFixture.Delta(8)));
        Assert.True(replica.HasMissedTicks);
    }

    [Fact]
    public void GarbageApplied_PushesTheStackUpAndLeavesOneHole()
    {
        var replica = new DuelReplica();
        replica.ApplySnapshot(DuelFixture.Snapshot(0, DuelFixture.EmptyCells().FillRow(0, 4)));

        replica.ApplyTickDelta(DuelFixture.Delta(1, DuelEvent.From(TickEvent.GarbageApplied(0, 2, 7))));

        var board = replica.Board1;
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < DuelReplicaBoard.Width; x++)
            {
                Assert.Equal(x == 7 ? CellKind.Empty : CellKind.Garbage, board.GetCell(x, y));
            }
        }

        // The row that was at the bottom is now two rows up, hole and all.
        for (var x = 0; x < DuelReplicaBoard.Width; x++)
        {
            Assert.Equal(x == 4 ? CellKind.Empty : CellKind.Garbage, board.GetCell(x, 2));
        }
    }

    [Fact]
    public void PieceLocked_ImprintsThePoseAndDropsTheRowsItCompleted()
    {
        var replica = new DuelReplica();
        replica.ApplySnapshot(DuelFixture.Snapshot(0, DuelFixture.EmptyCells().FillRow(0, 3, 4, 5, 6)));

        // A flat I with its rotation box origin two rows below the board fills exactly the gap.
        replica.ApplyTickDelta(DuelFixture.Delta(
            1,
            DuelEvent.From(TickEvent.PieceLocked(0, PieceKind.I, Rotation.Spawn, 3, -2))));

        foreach (var cell in replica.Board1.Cells.ToArray())
        {
            Assert.Equal(CellKind.Empty, cell);
        }
    }

    [Fact]
    public void PieceLocked_KeepsRowsItDidNotComplete()
    {
        var replica = new DuelReplica();
        replica.ApplySnapshot(DuelFixture.Snapshot(0, DuelFixture.EmptyCells().FillRow(0, 3, 4, 5, 6, 9)));

        replica.ApplyTickDelta(DuelFixture.Delta(
            1,
            DuelEvent.From(TickEvent.PieceLocked(0, PieceKind.I, Rotation.Spawn, 3, -2))));

        var board = replica.Board1;
        for (var x = 0; x < DuelReplicaBoard.Width; x++)
        {
            var expected = x switch
            {
                9 => CellKind.Empty,
                >= 3 and <= 6 => CellKind.I,
                _ => CellKind.Garbage,
            };

            Assert.Equal(expected, board.GetCell(x, 0));
        }
    }

    [Fact]
    public void PieceSpawned_AdvancesTheNextQueue()
    {
        var replica = new DuelReplica();
        replica.ApplySnapshot(DuelFixture.Snapshot(0, DuelFixture.EmptyCells()));

        replica.ApplyTickDelta(DuelFixture.Delta(
            1,
            DuelEvent.From(TickEvent.PieceSpawned(0, PieceKind.I, PieceKind.L))));

        Assert.Equal([PieceKind.O, PieceKind.T, PieceKind.S, PieceKind.Z, PieceKind.L], replica.Board1.Next);
    }

    [Fact]
    public void HoldSwapped_SpendsTheAllowanceAfterTheSpawnItCaused()
    {
        var replica = new DuelReplica();
        replica.ApplySnapshot(DuelFixture.Snapshot(0, DuelFixture.EmptyCells()));

        // An empty hold slot swallows the piece and spawns the next one; the spawn must not re-arm hold.
        replica.ApplyTickDelta(DuelFixture.Delta(
            1,
            DuelEvent.From(TickEvent.HoldSwapped(0, PieceKind.T, PieceKind.I)),
            DuelEvent.From(TickEvent.PieceSpawned(0, PieceKind.I, PieceKind.L))));

        Assert.Equal(PieceKind.T, replica.Board1.Hold);
        Assert.False(replica.Board1.HoldAvailable);

        // The next spawn is an ordinary one and does re-arm it.
        replica.ApplyTickDelta(DuelFixture.Delta(
            2,
            DuelEvent.From(TickEvent.PieceSpawned(0, PieceKind.O, PieceKind.J))));

        Assert.True(replica.Board1.HoldAvailable);
    }

    [Fact]
    public void ApplyMatchFinished_RecordsTheResult()
    {
        var replica = new DuelReplica();
        var result = new MatchResult(DuelOutcome.Player2Wins, new PlayerIndex(1), MatchEndReason.Forfeit);

        replica.ApplyMatchFinished(result);

        Assert.Equal(result, replica.Result);
    }
}
