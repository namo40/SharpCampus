using SharpCampus.BotServer.Bots;
using SharpCampus.GameCore;
using Xunit;

namespace SharpCampus.BotServer.Tests;

public class BotPlannerTests
{
    private const int WellColumn = 9;

    [Fact]
    public void PlanTurnsThePieceFirst_ThenWalksItToTheColumn_ThenDropsIt()
    {
        // Four rows filled but for the last column: nothing scores like the four-line clear, and the
        // only way to it is the piece stood on end above the well.
        var cells = Empty();
        FillRows(cells, 0, 3, WellColumn);

        Assert.Equal(
            [
                GameInput.RotateCw,
                GameInput.MoveRight,
                GameInput.MoveRight,
                GameInput.MoveRight,
                GameInput.MoveRight,
                GameInput.HardDrop,
            ],
            BotPlanner.Plan(cells, PieceKind.I));
    }

    [Fact]
    public void PlanForAnEmptyBoard_PutsThePieceWhereItLeavesTheFewestSteps()
    {
        // Anywhere in the middle leaves a step on both sides of the box; against the wall, only one.
        Assert.Equal(
            [GameInput.MoveLeft, GameInput.MoveLeft, GameInput.MoveLeft, GameInput.MoveLeft, GameInput.HardDrop],
            BotPlanner.Plan(Empty(), PieceKind.O));
    }

    [Theory]
    [InlineData(PieceKind.I)]
    [InlineData(PieceKind.O)]
    [InlineData(PieceKind.T)]
    [InlineData(PieceKind.S)]
    [InlineData(PieceKind.Z)]
    [InlineData(PieceKind.J)]
    [InlineData(PieceKind.L)]
    public void EveryPlanEndsInAHardDropAndUsesNothingTheBotDoesNotHave(PieceKind kind)
    {
        var plan = BotPlanner.Plan(Empty(), kind);

        Assert.NotEmpty(plan);
        Assert.Equal(GameInput.HardDrop, plan[^1]);
        Assert.DoesNotContain(GameInput.Hold, plan);
        Assert.DoesNotContain(GameInput.SoftDropOn, plan);
        Assert.DoesNotContain(GameInput.HardDrop, plan[..^1]);
    }

    [Fact]
    public void BoardWithNowhereLeftToDropIsPlayedAsNoInputsAtAll()
    {
        var cells = Empty();
        FillRows(cells, 0, BoardSimulation.Height - 1);

        Assert.Empty(BotPlanner.Plan(cells, PieceKind.T));
    }

    private static CellKind[] Empty() => new CellKind[BoardSimulation.Width * BoardSimulation.Height];

    private static void FillRows(CellKind[] cells, int fromY, int toY, params int[] holeColumns)
    {
        for (var y = fromY; y <= toY; y++)
        {
            for (var x = 0; x < BoardSimulation.Width; x++)
            {
                if (Array.IndexOf(holeColumns, x) < 0)
                {
                    cells[(y * BoardSimulation.Width) + x] = CellKind.Garbage;
                }
            }
        }
    }
}
