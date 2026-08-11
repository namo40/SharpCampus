using SharpCampus.GameCore.Internal;
using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void TheSameSeedAndInputsReplayIdentically()
    {
        var first = Run(seed: 0xA11CE);
        var second = Run(seed: 0xA11CE);

        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Cells1, second.Cells1);
        Assert.Equal(first.Cells2, second.Cells2);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.NotEmpty(first.Events);
    }

    [Fact]
    public void DifferentSeedsDealDifferentPieces()
    {
        var first = new DuelSimulation(TestConfigs.Default, seed: 1);
        var second = new DuelSimulation(TestConfigs.Default, seed: 2);

        Assert.NotEqual(first.Board1.Next.ToArray(), second.Board1.Next.ToArray());
    }

    [Fact]
    public void BothPlayersFaceTheSamePieceSequence()
    {
        var duel = new DuelSimulation(TestConfigs.Frozen, seed: 777);

        for (var tick = 0; tick < 20; tick++)
        {
            duel.Tick([GameInput.HardDrop], [GameInput.HardDrop]);
            Assert.Equal(duel.Board1.Next.ToArray(), duel.Board2.Next.ToArray());
            Assert.Equal(duel.Board1.ActivePiece?.Kind, duel.Board2.ActivePiece?.Kind);
        }
    }

    private static RunResult Run(ulong seed)
    {
        var duel = new DuelSimulation(TestConfigs.Default, seed);
        var random = new SplitMix64(seed);
        var events = new List<TickEvent>();

        for (var tick = 0; tick < 2000 && !duel.Finished; tick++)
        {
            var inputs1 = NextInputs(ref random);
            var inputs2 = NextInputs(ref random);
            events.AddRange(duel.Tick(inputs1, inputs2).Events);
        }

        return new RunResult(events, duel.Board1.Cells.ToArray(), duel.Board2.Cells.ToArray(), duel.Outcome);
    }

    private static GameInput[] NextInputs(ref SplitMix64 random)
    {
        var count = random.NextInt(3);
        var inputs = new GameInput[count];
        for (var i = 0; i < count; i++)
        {
            inputs[i] = (GameInput)random.NextInt(8);
        }

        return inputs;
    }

    private sealed record RunResult(
        List<TickEvent> Events,
        CellKind[] Cells1,
        CellKind[] Cells2,
        DuelOutcome Outcome);
}
