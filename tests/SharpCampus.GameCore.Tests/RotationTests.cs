using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class RotationTests
{
    [Fact]
    public void RotatesInOpenSpaceWithoutKicking()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);
        var spawned = board.Active();

        board.Tick([GameInput.RotateCw]);

        var rotated = board.Active();
        Assert.Equal(Rotation.Right, rotated.Rotation);
        Assert.Equal(spawned.X, rotated.X);
        Assert.Equal(spawned.Y, rotated.Y);
    }

    [Fact]
    public void RotatesBackToSpawnStateWithTwoOppositeRotations()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.T);
        board.Tick([]);

        board.Tick([GameInput.RotateCw, GameInput.RotateCcw]);

        Assert.Equal(Rotation.Spawn, board.Active().Rotation);
    }

    [Fact]
    public void RotationAgainstTheLeftWallSucceedsOnlyByKicking()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);

        board.Tick([GameInput.RotateCw]);
        board.Tick(
        [
            GameInput.MoveLeft, GameInput.MoveLeft, GameInput.MoveLeft,
            GameInput.MoveLeft, GameInput.MoveLeft, GameInput.MoveLeft,
        ]);

        var atWall = board.Active();
        Assert.Equal(-2, atWall.X);

        board.Tick([GameInput.RotateCcw]);

        // The unkicked placement would put cells at x = -2, so only the +2 kick can succeed.
        var kicked = board.Active();
        Assert.Equal(Rotation.Spawn, kicked.Rotation);
        Assert.Equal(0, kicked.X);
        Assert.Equal(atWall.Y, kicked.Y);
    }

    [Fact]
    public void RotationIsRejectedWhenEveryKickCollides()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);
        board.LoadCells(BoardFixture.Empty().FillRows(0, 19, 9).FillRow(21));
        var before = board.Active();

        board.Tick([GameInput.RotateCw]);

        var after = board.Active();
        Assert.Equal(before, after);
    }

    [Fact]
    public void RotatingTheOPieceChangesNothing()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.O);
        board.Tick([]);
        var before = board.Active();

        board.Tick([GameInput.RotateCw, GameInput.RotateCcw, GameInput.RotateCw]);

        Assert.Equal(before, board.Active());
    }

    [Theory]
    [InlineData(PieceKind.I)]
    [InlineData(PieceKind.O)]
    [InlineData(PieceKind.T)]
    [InlineData(PieceKind.S)]
    [InlineData(PieceKind.Z)]
    [InlineData(PieceKind.J)]
    [InlineData(PieceKind.L)]
    public void EveryRotationStateKeepsFourCellsInsideItsBox(PieceKind kind)
    {
        var box = Tetrominoes.GetBoxSize(kind);
        for (var rotation = Rotation.Spawn; rotation <= Rotation.Left; rotation++)
        {
            var cells = Tetrominoes.GetCells(kind, rotation);
            Assert.Equal(4, cells.Length);
            foreach (var cell in cells)
            {
                Assert.InRange(cell.X, 0, box - 1);
                Assert.InRange(cell.Y, 0, box - 1);
            }
        }
    }

    [Theory]
    [InlineData(PieceKind.I)]
    [InlineData(PieceKind.O)]
    [InlineData(PieceKind.T)]
    [InlineData(PieceKind.S)]
    [InlineData(PieceKind.Z)]
    [InlineData(PieceKind.J)]
    [InlineData(PieceKind.L)]
    public void EveryPieceSpawnsInsideTheHiddenRows(PieceKind kind)
    {
        var originX = Tetrominoes.GetSpawnX(kind);
        var originY = Tetrominoes.GetSpawnY(kind);
        foreach (var cell in Tetrominoes.GetCells(kind, Rotation.Spawn))
        {
            Assert.InRange(originX + cell.X, 0, BoardSimulation.Width - 1);
            Assert.InRange(originY + cell.Y, BoardSimulation.VisibleHeight, BoardSimulation.Height - 1);
        }
    }
}
