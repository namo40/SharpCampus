using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class LockdownTests
{
    // With one cell of gravity per tick the piece grounds on the floor at this tick.
    private const int GroundedTick = 20;

    [Fact]
    public void GroundedPieceLocksAfterTheLockDelay()
    {
        var board = TestBoard.Create(TestConfigs.Default.WithGravity(1), PieceKind.I);

        var lockTick = board.RunUntil(TickEventKind.PieceLocked, maxTicks: 60);

        Assert.Equal(GroundedTick + 10 - 1, lockTick);
    }

    [Fact]
    public void LockDelayLengthComesFromTheConfig()
    {
        var board = TestBoard.Create(TestConfigs.Default.WithGravity(1).WithLockDelay(4), PieceKind.I);

        var lockTick = board.RunUntil(TickEventKind.PieceLocked, maxTicks: 60);

        Assert.Equal(GroundedTick + 4 - 1, lockTick);
    }

    [Fact]
    public void SuccessfulMovesResetTheDelayUntilTheResetCapIsSpent()
    {
        var board = TestBoard.Create(TestConfigs.Default.WithGravity(1), PieceKind.I);
        board.Advance(GroundedTick);
        Assert.False(board.View.ActivePiece is null);

        var lockTick = -1;
        for (var tick = GroundedTick + 1; tick <= GroundedTick + 30 && lockTick < 0; tick++)
        {
            board.Tick([tick % 2 == 1 ? GameInput.MoveLeft : GameInput.MoveRight]);
            if (board.LastTickEvents.Has(TickEventKind.PieceLocked))
            {
                lockTick = tick;
            }
        }

        // The 15 allowed resets postpone the lock, then grounding locks the piece with no delay left.
        Assert.Equal(GroundedTick + 15, lockTick);
    }

    [Fact]
    public void ResetCapLengthComesFromTheConfig()
    {
        var board = TestBoard.Create(TestConfigs.Default.WithGravity(1).WithLockResetMax(3), PieceKind.I);
        board.Advance(GroundedTick);

        var lockTick = -1;
        for (var tick = GroundedTick + 1; tick <= GroundedTick + 30 && lockTick < 0; tick++)
        {
            board.Tick([tick % 2 == 1 ? GameInput.MoveLeft : GameInput.MoveRight]);
            if (board.LastTickEvents.Has(TickEventKind.PieceLocked))
            {
                lockTick = tick;
            }
        }

        Assert.Equal(GroundedTick + 3, lockTick);
    }

    [Fact]
    public void HardDropLocksInTheSameTick()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);

        board.Tick([GameInput.HardDrop]);

        Assert.True(board.LastTickEvents.Has(TickEventKind.PieceLocked));
        Assert.True(board.LastTickEvents.Has(TickEventKind.PieceSpawned));
        for (var x = 3; x <= 6; x++)
        {
            Assert.Equal(CellKind.I, board.View.GetCell(x, 0));
        }
    }

    [Fact]
    public void OnlyOneLockHappensPerTick()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);

        board.Tick([GameInput.HardDrop, GameInput.HardDrop, GameInput.HardDrop]);

        Assert.Single(board.LastTickEvents.All(TickEventKind.PieceLocked));
    }
}
