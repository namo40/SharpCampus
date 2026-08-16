using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class PlacementsTests
{
    private const int WellColumn = 9;

    [Theory]
    [InlineData(PieceKind.I, 34)]
    [InlineData(PieceKind.O, 36)]
    [InlineData(PieceKind.T, 34)]
    [InlineData(PieceKind.S, 34)]
    [InlineData(PieceKind.Z, 34)]
    [InlineData(PieceKind.J, 34)]
    [InlineData(PieceKind.L, 34)]
    public void EveryColumnAPieceFitsInIsACandidate(PieceKind kind, int expected)
    {
        // One pose per column the shape fits in, per rotation: four wide leaves seven columns, one wide
        // leaves ten. Symmetric rotations repeat poses, and those duplicates are counted too.
        Assert.Equal(expected, Placements.Enumerate(BoardFixture.Empty(), kind).Count);
    }

    [Theory]
    [InlineData(PieceKind.I)]
    [InlineData(PieceKind.O)]
    [InlineData(PieceKind.T)]
    [InlineData(PieceKind.S)]
    [InlineData(PieceKind.Z)]
    [InlineData(PieceKind.J)]
    [InlineData(PieceKind.L)]
    public void CandidatesStayOnTheBoardAndReachTheFloor(PieceKind kind)
    {
        foreach (var placement in Placements.Enumerate(BoardFixture.Empty(), kind))
        {
            var lowest = int.MaxValue;

            foreach (var cell in Tetrominoes.GetCells(kind, placement.Rotation))
            {
                Assert.InRange(placement.X + cell.X, 0, BoardSimulation.Width - 1);
                Assert.InRange(placement.Y + cell.Y, 0, BoardSimulation.Height - 1);
                lowest = Math.Min(lowest, placement.Y + cell.Y);
            }

            // Nothing is under it, so a straight drop can only end on the floor.
            Assert.Equal(0, lowest);
        }
    }

    [Fact]
    public void AnEmptyBoardMeasuresZero()
    {
        Assert.Equal(default, Placements.Evaluate(BoardFixture.Empty()));
    }

    [Fact]
    public void ColumnHeightsAddUpAndTheirStepsAreTheBumpiness()
    {
        var cells = BoardFixture.Empty().FillColumn(0, 0, 2).FillColumn(2, 0, 4);

        var metrics = Placements.Evaluate(cells);

        Assert.Equal(8, metrics.AggregateHeight);
        Assert.Equal(0, metrics.Holes);

        // |3-0| going into the gap, |0-5| coming out of it, |5-0| off the second tower.
        Assert.Equal(13, metrics.Bumpiness);
    }

    [Fact]
    public void EveryCellUnderAFilledOneIsAHole()
    {
        // A full row floating two rows above the floor: ten columns three cells tall, twenty buried.
        var cells = BoardFixture.Empty().FillRow(2);

        var metrics = Placements.Evaluate(cells);

        Assert.Equal(30, metrics.AggregateHeight);
        Assert.Equal(20, metrics.Holes);
        Assert.Equal(0, metrics.Bumpiness);
    }

    [Fact]
    public void ARowCompletedByThePieceIsCleared()
    {
        var cells = BoardFixture.Empty().FillRow(0, WellColumn);

        var clearing = Placements.Enumerate(cells, PieceKind.I).FindAll(placement => placement.LinesCleared > 0);

        // The two rotations that stand the piece up in the well, both clearing the row it completes and
        // leaving the three cells that were above it standing in that column.
        Assert.NotEmpty(clearing);
        Assert.All(clearing, placement => Assert.Equal(1, placement.LinesCleared));
        Assert.All(clearing, placement => Assert.Equal(new BoardMetrics(3, 0, 3), placement.After));
    }

    [Fact]
    public void MetricsDescribeTheBoardTheClearLeavesBehind()
    {
        var cells = BoardFixture.Empty().FillRows(0, 3, WellColumn);

        var clearing = Placements.Enumerate(cells, PieceKind.I).FindAll(placement => placement.LinesCleared > 0);

        Assert.NotEmpty(clearing);
        Assert.All(clearing, placement => Assert.Equal(4, placement.LinesCleared));

        // Everything the board held went with the four rows, so the piece is measured against nothing.
        Assert.All(clearing, placement => Assert.Equal(default, placement.After));
    }

    [Fact]
    public void AColumnThatReachesTheCeilingTakesNoPiece()
    {
        var cells = BoardFixture.Empty().FillColumn(0, 0, BoardSimulation.Height - 1);

        // The O box is two wide, so the buried column takes the pose beside it with it.
        var candidates = Placements.Enumerate(cells, PieceKind.O);

        Assert.All(candidates, placement => Assert.True(placement.X > 0));
    }
}
