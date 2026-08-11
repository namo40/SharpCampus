using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class TopOutTests
{
    [Fact]
    public void SpawningIntoOccupiedCellsTopsOut()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);
        board.LoadCells(SpawnBlockedStack());

        board.Tick([GameInput.HardDrop]);

        Assert.True(board.ToppedOut);
        Assert.True(board.LastTickEvents.Has(TickEventKind.ToppedOut));
    }

    [Fact]
    public void GarbagePushingBlocksPastTheTopRowTopsOut()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);
        board.LoadCells(FullHeightColumn());
        board.ReceiveGarbage(1);

        board.Tick([GameInput.HardDrop]);

        Assert.True(board.ToppedOut);
        Assert.True(board.LastTickEvents.Has(TickEventKind.GarbageApplied));
        Assert.True(board.LastTickEvents.Has(TickEventKind.ToppedOut));
    }

    [Fact]
    public void AToppedOutBoardStopsAdvancing()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);
        board.LoadCells(SpawnBlockedStack());
        board.Tick([GameInput.HardDrop]);

        var elapsed = board.View.ElapsedTicks;
        board.Tick([GameInput.HardDrop]);

        Assert.Equal(elapsed, board.View.ElapsedTicks);
        Assert.Empty(board.LastTickEvents);
    }

    [Fact]
    public void TheSurvivingPlayerWinsTheDuel()
    {
        var duel = TestBoard.CreateDuel(TestConfigs.Frozen, PieceKind.I);
        duel.Tick([], []);
        duel.GetBoard(1).LoadCells(SpawnBlockedStack());

        var result = duel.Tick([], [GameInput.HardDrop]);

        Assert.True(result.Finished);
        Assert.Equal(DuelOutcome.Player1Wins, result.Outcome);
    }

    [Fact]
    public void OnASimultaneousTopOutTheGarbageReceiverLoses()
    {
        var duel = TestBoard.CreateDuel(TestConfigs.Frozen, PieceKind.I);
        duel.Tick([], []);
        duel.GetBoard(0).LoadCells(FullHeightColumn());
        duel.GetBoard(0).ReceiveGarbage(1);
        duel.GetBoard(1).LoadCells(SpawnBlockedStack());

        var result = duel.Tick([GameInput.HardDrop], [GameInput.HardDrop]);

        Assert.True(duel.Board1.ToppedOut);
        Assert.True(duel.Board2.ToppedOut);
        Assert.Equal(DuelOutcome.Player2Wins, result.Outcome);
    }

    [Fact]
    public void ASimultaneousTopOutWithoutGarbageIsADraw()
    {
        var duel = TestBoard.CreateDuel(TestConfigs.Frozen, PieceKind.I);
        duel.Tick([], []);
        duel.GetBoard(0).LoadCells(SpawnBlockedStack());
        duel.GetBoard(1).LoadCells(SpawnBlockedStack());

        var result = duel.Tick([GameInput.HardDrop], [GameInput.HardDrop]);

        Assert.Equal(DuelOutcome.Draw, result.Outcome);
    }

    [Fact]
    public void AFinishedDuelIgnoresFurtherTicks()
    {
        var duel = TestBoard.CreateDuel(TestConfigs.Frozen, PieceKind.I);
        duel.Tick([], []);
        duel.GetBoard(1).LoadCells(SpawnBlockedStack());
        var finishedAt = duel.Tick([], [GameInput.HardDrop]).TickNumber;

        var result = duel.Tick([GameInput.HardDrop], [GameInput.HardDrop]);

        Assert.Equal(finishedAt, result.TickNumber);
        Assert.Empty(result.Events);
        Assert.Equal(DuelOutcome.Player1Wins, result.Outcome);
    }

    // Everything below the spawn rows is filled, but no row is complete, so the piece locks
    // where it stands and the next spawn has nowhere to go.
    private static CellKind[] SpawnBlockedStack()
        => BoardFixture.Empty().FillRows(0, 19, 9).FillRow(20, 3, 4, 5, 6, 9);

    private static CellKind[] FullHeightColumn()
        => BoardFixture.Empty().FillColumn(0, 0, BoardSimulation.Height - 1);
}
