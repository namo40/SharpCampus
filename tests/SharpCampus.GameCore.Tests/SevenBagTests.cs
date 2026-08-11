using SharpCampus.GameCore.Internal;
using Xunit;

namespace SharpCampus.GameCore.Tests;

public sealed class SevenBagTests
{
    [Fact]
    public void EveryBagDealsEachKindExactlyOnce()
    {
        var source = new SevenBagPieceSource(0xC0FFEE);

        for (var bag = 0; bag < 20; bag++)
        {
            var dealt = new HashSet<PieceKind>();
            for (var i = 0; i < Tetrominoes.KindCount; i++)
            {
                Assert.True(dealt.Add(source.Next()));
            }
        }
    }

    [Fact]
    public void BagsAreShuffledRatherThanDealtInOrder()
    {
        var source = new SevenBagPieceSource(0xC0FFEE);
        var ordered = new[] { PieceKind.I, PieceKind.O, PieceKind.T, PieceKind.S, PieceKind.Z, PieceKind.J, PieceKind.L };

        var dealt = new PieceKind[Tetrominoes.KindCount];
        for (var i = 0; i < dealt.Length; i++)
        {
            dealt[i] = source.Next();
        }

        Assert.NotEqual(ordered, dealt);
    }

    [Fact]
    public void TheSimulationFeedsPiecesInBags()
    {
        var board = new BoardSimulation(TestConfigs.Frozen, seed: 7);
        var dealt = new List<PieceKind>(board.View.Next);

        for (var tick = 0; tick < 30; tick++)
        {
            board.LoadCells(BoardFixture.Empty());
            board.Tick([GameInput.HardDrop]);
            foreach (var spawned in board.LastTickEvents.All(TickEventKind.PieceSpawned))
            {
                dealt.Add((PieceKind)spawned.Extra);
            }
        }

        var bags = dealt.Count / Tetrominoes.KindCount;
        Assert.True(bags >= 4);
        for (var bag = 0; bag < bags; bag++)
        {
            var kinds = new HashSet<PieceKind>();
            for (var i = 0; i < Tetrominoes.KindCount; i++)
            {
                kinds.Add(dealt[(bag * Tetrominoes.KindCount) + i]);
            }

            Assert.Equal(Tetrominoes.KindCount, kinds.Count);
        }
    }
}
