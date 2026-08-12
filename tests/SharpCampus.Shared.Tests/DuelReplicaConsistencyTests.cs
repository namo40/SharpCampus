using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using Xunit;

namespace SharpCampus.Shared.Tests;

// The contract the whole synchronisation layer rests on: a client that only ever sees the broadcast
// messages ends up with the server's boards, cell for cell.
public class DuelReplicaConsistencyTests
{
    private const int TickBudget = 20_000;
    private const int CompareEvery = 16;

    [Theory]
    [InlineData(1u)]
    [InlineData(20260812u)]
    [InlineData(ulong.MaxValue)]
    public void Replica_MatchesTheSimulation_ForAWholeDuel(ulong seed)
    {
        var simulation = new DuelSimulation(DuelFixture.Config, seed);
        var replica = new DuelReplica();
        replica.ApplySnapshot(TickDeltaFactory.CreateSnapshot(
            simulation.TickNumber,
            simulation.Board1,
            simulation.Board2));

        var random = new Random((int)(seed % int.MaxValue));
        var ticks = 0;

        while (!simulation.Finished && ticks < TickBudget)
        {
            ticks++;
            var result = simulation.Tick(DuelFixture.RandomInputs(random), DuelFixture.RandomInputs(random));

            Assert.True(replica.ApplyTickDelta(
                TickDeltaFactory.Create(result, simulation.Board1, simulation.Board2)));

            if (ticks % CompareEvery == 0)
            {
                AssertBoards(simulation, replica);
            }
        }

        Assert.True(simulation.Finished);
        Assert.False(replica.HasMissedTicks);
        Assert.Equal(simulation.TickNumber, replica.Tick.AsPrimitive());
        AssertBoards(simulation, replica);
    }

    [Fact]
    public void Replica_RebuiltFromASnapshotMidMatch_MatchesTheSimulation()
    {
        var simulation = new DuelSimulation(DuelFixture.Config, 7u);
        var random = new Random(7);

        for (var tick = 0; tick < 300 && !simulation.Finished; tick++)
        {
            simulation.Tick(DuelFixture.RandomInputs(random), DuelFixture.RandomInputs(random));
        }

        var replica = new DuelReplica();
        replica.ApplySnapshot(TickDeltaFactory.CreateSnapshot(
            simulation.TickNumber,
            simulation.Board1,
            simulation.Board2));

        AssertBoards(simulation, replica);

        // And it keeps up from there.
        for (var tick = 0; tick < 100 && !simulation.Finished; tick++)
        {
            var result = simulation.Tick(DuelFixture.RandomInputs(random), DuelFixture.RandomInputs(random));
            Assert.True(replica.ApplyTickDelta(
                TickDeltaFactory.Create(result, simulation.Board1, simulation.Board2)));
        }

        AssertBoards(simulation, replica);
    }

    private static void AssertBoards(DuelSimulation simulation, DuelReplica replica)
    {
        DuelFixture.AssertMatches(simulation.Board1, replica.Board1);
        DuelFixture.AssertMatches(simulation.Board2, replica.Board2);
    }
}
