using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class LineClearTests
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

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 4)]
    public void ClearingLinesSendsTheConfiguredAttack(int rows, int expectedAttack)
    {
        var board = NewBoard();
        board.LoadCells(BoardFixture.Empty().FillRows(0, rows - 1, WellColumn));

        var attack = board.Tick(_dropIntoWell);

        var cleared = board.LastTickEvents.Single(TickEventKind.LinesCleared);
        Assert.Equal(rows, cleared.Value);
        Assert.Equal(1, cleared.Extra);
        Assert.Equal(expectedAttack, attack);

        if (expectedAttack == 0)
        {
            Assert.False(board.LastTickEvents.Has(TickEventKind.GarbageSent));
        }
        else
        {
            Assert.Equal(expectedAttack, board.LastTickEvents.Single(TickEventKind.GarbageSent).Value);
        }
    }

    [Fact]
    public void ClearedRowsAreRemovedFromTheBoard()
    {
        var board = NewBoard();
        board.LoadCells(BoardFixture.Empty().FillRows(0, 3, WellColumn));

        board.Tick(_dropIntoWell);

        for (var y = 0; y < BoardSimulation.Height; y++)
        {
            for (var x = 0; x < BoardSimulation.Width; x++)
            {
                Assert.Equal(CellKind.Empty, board.View.GetCell(x, y));
            }
        }
    }

    [Fact]
    public void ConsecutiveClearsRaiseTheComboBonus()
    {
        var board = NewBoard();

        var attacks = new List<int>();
        for (var i = 0; i < 8; i++)
        {
            board.LoadCells(BoardFixture.Empty().FillRows(0, 3, WellColumn));
            attacks.Add(board.Tick(_dropIntoWell));
        }

        Assert.Equal([4, 5, 5, 6, 6, 7, 7, 7], attacks);
    }

    [Fact]
    public void ANonClearingLockResetsTheCombo()
    {
        var board = NewBoard();
        board.LoadCells(BoardFixture.Empty().FillRows(0, 3, WellColumn));
        board.Tick(_dropIntoWell);
        Assert.Equal(1, board.View.Combo);

        board.LoadCells(BoardFixture.Empty());
        board.Tick([GameInput.HardDrop]);
        Assert.Equal(0, board.View.Combo);

        board.LoadCells(BoardFixture.Empty().FillRows(0, 3, WellColumn));
        var attack = board.Tick(_dropIntoWell);

        Assert.Equal(1, board.View.Combo);
        Assert.Equal(4, attack);
    }

    private static BoardSimulation NewBoard()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);
        return board;
    }
}
