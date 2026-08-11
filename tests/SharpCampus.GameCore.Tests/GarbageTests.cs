using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class GarbageTests
{
    private static readonly GameInput[] _dropIntoWell =
    [
        GameInput.RotateCw,
        GameInput.MoveRight,
        GameInput.MoveRight,
        GameInput.MoveRight,
        GameInput.MoveRight,
        GameInput.HardDrop,
    ];

    [Fact]
    public void OutgoingAttackCancelsPendingGarbageFirst()
    {
        var board = NewBoard();
        board.ReceiveGarbage(3);
        board.LoadCells(BoardFixture.Empty().FillRows(0, 3, 9));

        var attack = board.Tick(_dropIntoWell);

        Assert.Equal(1, attack);
        Assert.Equal(1, board.LastTickEvents.Single(TickEventKind.GarbageSent).Value);
        Assert.Equal(0, board.View.PendingGarbage);
    }

    [Fact]
    public void AnAttackSmallerThanThePendingQueueSendsNothing()
    {
        var board = NewBoard();
        board.ReceiveGarbage(6);
        board.LoadCells(BoardFixture.Empty().FillRows(0, 3, 9));

        var attack = board.Tick(_dropIntoWell);

        Assert.Equal(0, attack);
        Assert.False(board.LastTickEvents.Has(TickEventKind.GarbageSent));
        Assert.Equal(2, board.View.PendingGarbage);
    }

    [Fact]
    public void ANonClearingLockAppliesGarbageUpToTheCap()
    {
        var board = NewBoard();
        board.ReceiveGarbage(20);

        board.Tick([GameInput.HardDrop]);

        var applied = board.LastTickEvents.Single(TickEventKind.GarbageApplied);
        Assert.Equal(8, applied.Value);
        Assert.Equal(12, board.View.PendingGarbage);

        for (var y = 0; y < 8; y++)
        {
            AssertGarbageRow(board, y, applied.Extra);
        }
    }

    [Fact]
    public void EveryRowOfOneChunkSharesTheSameHole()
    {
        var board = NewBoard();
        board.ReceiveGarbage(3);
        board.ReceiveGarbage(2);

        board.Tick([GameInput.HardDrop]);

        var applied = board.LastTickEvents.All(TickEventKind.GarbageApplied);
        Assert.Equal(2, applied.Count);
        Assert.Equal(3, applied[0].Value);
        Assert.Equal(2, applied[1].Value);
        Assert.Equal(0, board.View.PendingGarbage);

        // The second chunk was pushed in underneath the first one.
        for (var y = 0; y < 2; y++)
        {
            AssertGarbageRow(board, y, applied[1].Extra);
        }

        for (var y = 2; y < 5; y++)
        {
            AssertGarbageRow(board, y, applied[0].Extra);
        }
    }

    [Fact]
    public void GarbageInsertionLiftsTheExistingStack()
    {
        var board = NewBoard();
        board.Tick([GameInput.HardDrop]);
        Assert.Equal(CellKind.I, board.View.GetCell(3, 0));

        board.ReceiveGarbage(2);
        board.Tick([GameInput.HardDrop]);

        Assert.Equal(CellKind.I, board.View.GetCell(3, 2));
    }

    [Fact]
    public void GarbageDrawsDoNotPerturbThePieceSequence()
    {
        var quiet = new BoardSimulation(TestConfigs.Frozen, seed: 4242);
        var attacked = new BoardSimulation(TestConfigs.Frozen, seed: 4242);
        attacked.ReceiveGarbage(2);
        attacked.ReceiveGarbage(3);

        for (var i = 0; i < 6; i++)
        {
            quiet.LoadCells(BoardFixture.Empty());
            attacked.LoadCells(BoardFixture.Empty());
            quiet.Tick([GameInput.HardDrop]);
            attacked.Tick([GameInput.HardDrop]);

            Assert.Equal(quiet.View.Next.ToArray(), attacked.View.Next.ToArray());
            Assert.Equal(quiet.View.ActivePiece, attacked.View.ActivePiece);
        }
    }

    private static void AssertGarbageRow(BoardSimulation board, int y, int holeColumn)
    {
        for (var x = 0; x < BoardSimulation.Width; x++)
        {
            var expected = x == holeColumn ? CellKind.Empty : CellKind.Garbage;
            Assert.Equal(expected, board.View.GetCell(x, y));
        }
    }

    private static BoardSimulation NewBoard()
    {
        var board = TestBoard.Create(TestConfigs.Frozen, PieceKind.I);
        board.Tick([]);
        return board;
    }
}
