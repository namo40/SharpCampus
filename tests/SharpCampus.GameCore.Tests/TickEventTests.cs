using Xunit;

namespace SharpCampus.GameCore.Tests;

public class TickEventTests
{
    [Theory]
    [InlineData(PieceKind.I, Rotation.Spawn, 3, 18)]
    [InlineData(PieceKind.O, Rotation.Half, 0, 0)]
    [InlineData(PieceKind.T, Rotation.Left, 9, 21)]
    // Rotation boxes hang off the board, so a locked origin is routinely negative.
    [InlineData(PieceKind.I, Rotation.Right, -2, 0)]
    [InlineData(PieceKind.S, Rotation.Half, -1, -1)]
    public void PieceLocked_ReadsBackThePoseItPacked(PieceKind kind, Rotation rotation, int x, int y)
    {
        var tickEvent = TickEvent.PieceLocked(1, kind, rotation, x, y);

        Assert.Equal(TickEventKind.PieceLocked, tickEvent.Kind);
        Assert.Equal(1, tickEvent.PlayerIndex);
        Assert.Equal(kind, tickEvent.LockedKind);
        Assert.Equal(rotation, tickEvent.LockedRotation);
        Assert.Equal(x, tickEvent.LockedX);
        Assert.Equal(y, tickEvent.LockedY);
    }

    [Fact]
    public void PieceLocked_ReportsThePoseTheBoardImprinted()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.T);
        board.Tick([GameInput.MoveLeft, GameInput.RotateCw, GameInput.HardDrop]);

        var locked = board.LastTickEvents.Single(TickEventKind.PieceLocked);
        var cells = Tetrominoes.GetCells(locked.LockedKind, locked.LockedRotation);
        for (var i = 0; i < cells.Length; i++)
        {
            Assert.Equal(
                Tetrominoes.ToCell(locked.LockedKind),
                board.View.GetCell(locked.LockedX + cells[i].X, locked.LockedY + cells[i].Y));
        }
    }
}
