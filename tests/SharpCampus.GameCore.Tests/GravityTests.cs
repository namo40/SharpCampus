using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class GravityTests
{
    [Fact]
    public void AtLevelOneThePieceFallsOneCellEveryTwentyTicks()
    {
        var board = TestBoard.Create(TestConfigs.Default, PieceKind.I);
        board.Tick([]);
        var spawnY = board.Active().Y;

        board.Advance(18);
        Assert.Equal(spawnY, board.Active().Y);

        board.Advance(1);
        Assert.Equal(spawnY - 1, board.Active().Y);

        board.Advance(20);
        Assert.Equal(spawnY - 2, board.Active().Y);
    }

    [Fact]
    public void SoftDropMakesThePieceFallEveryTick()
    {
        var board = TestBoard.Create(TestConfigs.Default, PieceKind.I);
        board.Tick([]);
        var y = board.Active().Y;

        board.Tick([GameInput.SoftDropOn]);
        Assert.True(board.View.SoftDropActive);
        Assert.Equal(y - 1, board.Active().Y);

        for (var i = 2; i <= 5; i++)
        {
            board.Tick([]);
            Assert.Equal(y - i, board.Active().Y);
        }

        board.Tick([GameInput.SoftDropOff]);
        Assert.False(board.View.SoftDropActive);
        Assert.Equal(y - 5, board.Active().Y);
    }

    [Fact]
    public void LevelRisesWithElapsedTicks()
    {
        var board = TestBoard.Create(TestConfigs.Default, PieceKind.I);

        board.Advance(599);
        Assert.Equal(1, board.View.Level);

        board.Advance(1);
        Assert.Equal(2, board.View.Level);
        Assert.Equal(600, board.View.ElapsedTicks);
    }

    [Fact]
    public void LevelIsCappedByTheConfig()
    {
        var config = TestConfigs.Default;

        Assert.Equal(config.MaxLevel, config.LevelAt(config.LevelUpIntervalTicks * 1000));
        Assert.Equal(20, config.GravityIntervalTicks(1));
        Assert.Equal(10, config.GravityIntervalTicks(5));
        Assert.Equal(4, config.GravityIntervalTicks(10));
        Assert.Equal(1, config.GravityIntervalTicks(15));
    }

    [Fact]
    public void GravitySpeedsUpWhenTheLevelRises()
    {
        var config = TestConfigs.Default.WithGravity(4, 2).WithLevelUpInterval(30);
        var board = TestBoard.Create(config, PieceKind.I);
        board.Tick([]);
        var spawnY = board.Active().Y;

        board.Advance(27);
        Assert.Equal(1, board.View.Level);
        Assert.Equal(spawnY - 7, board.Active().Y);

        board.Advance(2);
        Assert.Equal(2, board.View.Level);
        Assert.Equal(spawnY - 8, board.Active().Y);

        board.Advance(2);
        Assert.Equal(spawnY - 9, board.Active().Y);
    }
}
