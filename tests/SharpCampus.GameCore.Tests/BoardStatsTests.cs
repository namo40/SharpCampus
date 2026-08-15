using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class BoardStatsTests
{
    // Stands the I piece up in the rightmost column and drops it into the well the fixtures leave open.
    private static readonly GameInput[] _dropIntoWell =
    [
        GameInput.RotateCw,
        GameInput.MoveRight,
        GameInput.MoveRight,
        GameInput.MoveRight,
        GameInput.MoveRight,
        GameInput.HardDrop,
    ];

    private const int WellColumn = 9;

    [Fact]
    public void EveryHardDropIsCounted()
    {
        var board = NewBoard();

        for (var i = 0; i < 5; i++)
        {
            board.Tick([GameInput.HardDrop]);
        }

        Assert.Equal(new BoardStats(LinesCleared: 0, Quads: 0, GarbageSent: 0, HardDrops: 5, MaxCombo: 0), board.View.Stats);
    }

    [Fact]
    public void FourLineClearsAreCountedApartFromTheLinesThemselves()
    {
        var board = NewBoard();

        board.LoadCells(BoardFixture.Empty().FillRows(0, 3, WellColumn));
        board.Tick(_dropIntoWell);

        board.LoadCells(BoardFixture.Empty().FillRows(0, 1, WellColumn));
        board.Tick(_dropIntoWell);

        var stats = board.View.Stats;
        Assert.Equal(6, stats.LinesCleared);
        Assert.Equal(1, stats.Quads);
        Assert.Equal(2, stats.HardDrops);
    }

    [Fact]
    public void TheLongestComboIsKeptOnceItHasBeenBroken()
    {
        var board = NewBoard();

        for (var i = 0; i < 3; i++)
        {
            board.LoadCells(BoardFixture.Empty().FillRows(0, 3, WellColumn));
            board.Tick(_dropIntoWell);
        }

        board.LoadCells(BoardFixture.Empty());
        board.Tick([GameInput.HardDrop]);
        Assert.Equal(0, board.View.Combo);

        board.LoadCells(BoardFixture.Empty().FillRows(0, 3, WellColumn));
        board.Tick(_dropIntoWell);

        var stats = board.View.Stats;
        Assert.Equal(3, stats.MaxCombo);
        Assert.Equal(1, board.View.Combo);
        Assert.Equal(16, stats.LinesCleared);
        Assert.Equal(4, stats.Quads);
        Assert.Equal(5, stats.HardDrops);
    }

    [Fact]
    public void OnlyTheGarbageThatSurvivedTheOffsetCountsAsSent()
    {
        var board = NewBoard();
        board.ReceiveGarbage(3);
        board.LoadCells(BoardFixture.Empty().FillRows(0, 3, WellColumn));

        var attack = board.Tick(_dropIntoWell);

        Assert.Equal(1, attack);
        Assert.Equal(1, board.View.Stats.GarbageSent);
    }

    [Fact]
    public void EachBoardOfADuelKeepsItsOwnCount()
    {
        var duel = TestBoard.CreateDuel(TestConfigs.Frozen, PieceKind.I);

        duel.Tick([GameInput.HardDrop], []);
        duel.Tick([GameInput.HardDrop], [GameInput.HardDrop]);

        Assert.Equal(2, duel.Board1.Stats.HardDrops);
        Assert.Equal(1, duel.Board2.Stats.HardDrops);
    }

    private static BoardSimulation NewBoard()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);
        return board;
    }
}
