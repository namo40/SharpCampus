using SharpCampus.RoomServer.MasterData;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.MasterData.Import;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public class SimulationConfigFactoryTests
{
    // The sources are linked into the output of everything that references Server.Common, this suite included.
    private static MemoryDatabase Database { get; } =
        new(MasterDataImporter.Build(Path.Combine(AppContext.BaseDirectory, "masterdata")));

    [Fact]
    public void Create_CopiesTheTickCountsStraightAcross()
    {
        var config = Database.GameConfigTable.All[0];

        var rules = SimulationConfigFactory.Create(Database);

        Assert.Equal(config.TickRate, rules.TickRate);
        Assert.Equal(config.InputPerTickMax, rules.InputPerTickMax);
        Assert.Equal(config.NextCount, rules.NextCount);
        Assert.Equal(config.LockDelayTicks, rules.Simulation.LockDelayTicks);
        Assert.Equal(config.LockResetMax, rules.Simulation.LockResetMax);
        Assert.Equal(config.GarbageCapPerLock, rules.Simulation.GarbageCapPerLock);
        Assert.Equal(config.MaxLevel, rules.Simulation.MaxLevel);
    }

    [Fact]
    public void Create_ConvertsEverySecondsColumnIntoTicks()
    {
        var config = Database.GameConfigTable.All[0];

        var rules = SimulationConfigFactory.Create(Database);

        Assert.Equal(config.LevelUpIntervalSec * config.TickRate, rules.Simulation.LevelUpIntervalTicks);
        Assert.Equal(config.CountdownSec * config.TickRate, rules.CountdownTicks);
        Assert.Equal(config.JoinTimeoutSec * config.TickRate, rules.JoinTimeoutTicks);
    }

    [Fact]
    public void Create_OrdersTheGravityCurveByLevel()
    {
        var rules = SimulationConfigFactory.Create(Database);

        Assert.Equal(Database.GravityCurveTable.Count, rules.Simulation.GravityTicksPerCell.Count);
        for (var level = 1; level <= rules.Simulation.MaxLevel; level++)
        {
            Assert.Equal(
                Database.GravityCurveTable.FindByLevel(level).TicksPerCell,
                rules.Simulation.GravityIntervalTicks(level));
        }
    }

    [Fact]
    public void Create_OrdersTheAttackTableByLinesCleared()
    {
        var rules = SimulationConfigFactory.Create(Database);

        for (var lines = 1; lines <= rules.Simulation.AttackByLines.Count; lines++)
        {
            Assert.Equal(
                Database.AttackTableTable.FindByLinesCleared(lines).Garbage,
                rules.Simulation.AttackFor(lines, combo: 1));
        }
    }

    [Fact]
    public void Create_MapsEveryComboRange()
    {
        var rules = SimulationConfigFactory.Create(Database);

        Assert.Equal(Database.ComboTableTable.Count, rules.Simulation.ComboBonus.Count);
        foreach (var row in Database.ComboTableTable.All)
        {
            Assert.Equal(row.Bonus, rules.Simulation.ComboBonusFor(row.ComboMin));
            Assert.Equal(row.Bonus, rules.Simulation.ComboBonusFor(row.ComboMax));
        }
    }
}
