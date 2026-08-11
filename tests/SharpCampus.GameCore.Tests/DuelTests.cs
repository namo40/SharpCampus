using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class DuelTests
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
    public void AnAttackLandsInTheOpponentsQueue()
    {
        var duel = TestBoard.CreateDuel(TestConfigs.Frozen, PieceKind.I);
        duel.Tick([], []);
        duel.GetBoard(0).LoadCells(BoardFixture.Empty().FillRows(0, 3, 9));

        var result = duel.Tick(_dropIntoWell, []);

        var sent = result.Events.Single(TickEventKind.GarbageSent);
        Assert.Equal(0, sent.PlayerIndex);
        Assert.Equal(4, sent.Value);
        Assert.Equal(4, duel.Board2.PendingGarbage);
        Assert.Equal(0, duel.Board1.PendingGarbage);
    }

    [Fact]
    public void EventsCarryThePlayerTheyBelongTo()
    {
        var duel = TestBoard.CreateDuel(TestConfigs.Frozen, PieceKind.I);

        var result = duel.Tick([], []);

        Assert.Equal([0, 1], result.Events.All(TickEventKind.PieceSpawned).Select(spawned => spawned.PlayerIndex));
    }

    [Fact]
    public void InputsBeyondTheFloodCapAreDiscarded()
    {
        var duel = TestBoard.CreateDuel(TestConfigs.Frozen, PieceKind.T);
        duel.Tick([], []);

        duel.Tick(MoveLeftsThenRotate(DuelSimulation.MaxInputsPerTick), []);
        Assert.Equal(Rotation.Right, duel.Board1.ActivePiece!.Value.Rotation);

        duel.Tick(MoveLeftsThenRotate(DuelSimulation.MaxInputsPerTick + 1), []);
        Assert.Equal(Rotation.Right, duel.Board1.ActivePiece!.Value.Rotation);
    }

    [Fact]
    public void TickNumbersAdvanceOncePerTick()
    {
        var duel = TestBoard.CreateDuel(TestConfigs.Frozen, PieceKind.I);

        for (var expected = 1; expected <= 5; expected++)
        {
            Assert.Equal(expected, duel.Tick([], []).TickNumber);
        }

        Assert.Equal(5, duel.TickNumber);
        Assert.False(duel.Finished);
        Assert.Equal(DuelOutcome.Ongoing, duel.Outcome);
    }

    private static GameInput[] MoveLeftsThenRotate(int total)
    {
        var inputs = new GameInput[total];
        for (var i = 0; i < total - 1; i++)
        {
            inputs[i] = GameInput.MoveLeft;
        }

        inputs[total - 1] = GameInput.RotateCw;
        return inputs;
    }
}
