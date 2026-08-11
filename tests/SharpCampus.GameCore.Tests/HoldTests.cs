using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class HoldTests
{
    [Fact]
    public void HoldMovesTheActivePieceIntoTheEmptySlotAndSpawnsTheNextOne()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I, PieceKind.T);
        board.Tick([]);
        var queued = board.View.Next[0];

        board.Tick([GameInput.Hold]);

        Assert.Equal(PieceKind.I, board.View.Hold);
        Assert.Equal(queued, board.Active().Kind);
        Assert.False(board.View.HoldAvailable);

        var swap = board.LastTickEvents.Single(TickEventKind.HoldSwapped);
        Assert.Equal((int)PieceKind.I, swap.Value);
        Assert.Equal((int)queued, swap.Extra);
    }

    [Fact]
    public void SecondHoldBeforeLockingIsIgnored()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I, PieceKind.T);
        board.Tick([]);
        board.Tick([GameInput.Hold]);
        var afterFirstHold = board.Active();

        board.Tick([GameInput.Hold]);

        Assert.Equal(afterFirstHold, board.Active());
        Assert.Equal(PieceKind.I, board.View.Hold);
        Assert.False(board.LastTickEvents.Has(TickEventKind.HoldSwapped));
    }

    [Fact]
    public void HoldBecomesAvailableAgainAfterTheNextLock()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I, PieceKind.T, PieceKind.S);
        board.Tick([]);
        board.Tick([GameInput.Hold]);

        board.Tick([GameInput.HardDrop]);
        Assert.True(board.View.HoldAvailable);

        var active = board.Active().Kind;
        board.Tick([GameInput.Hold]);

        Assert.Equal(active, board.View.Hold);
        Assert.Equal(PieceKind.I, board.Active().Kind);
    }

    [Fact]
    public void HoldWithAFilledSlotSwapsWithoutTouchingTheNextQueue()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I, PieceKind.T, PieceKind.S);
        board.Tick([]);
        board.Tick([GameInput.Hold]);
        board.Tick([GameInput.HardDrop]);

        var nextBefore = board.View.Next.ToArray();
        var active = board.Active().Kind;

        board.Tick([GameInput.Hold]);

        Assert.Equal(nextBefore, board.View.Next.ToArray());
        Assert.Equal(active, board.View.Hold);
        Assert.False(board.LastTickEvents.Has(TickEventKind.PieceSpawned));
    }
}
