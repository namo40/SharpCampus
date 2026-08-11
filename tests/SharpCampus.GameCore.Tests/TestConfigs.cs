namespace SharpCampus.GameCore.Tests;

internal static class TestConfigs
{
    public static readonly int[] GravityCurve = [20, 17, 14, 12, 10, 9, 8, 7, 6, 4, 3, 3, 2, 2, 1];

    public static SimulationConfig Default => new(
        LockDelayTicks: 10,
        LockResetMax: 15,
        NextCount: 5,
        GarbageCapPerLock: 8,
        GravityTicksPerCell: GravityCurve,
        LevelUpIntervalTicks: 600,
        MaxLevel: 15,
        AttackByLines: [0, 1, 2, 4],
        ComboBonus: [new ComboBonusRange(2, 3, 1), new ComboBonusRange(4, 5, 2), new ComboBonusRange(6, int.MaxValue, 3)]);

    // Pieces hang in place long enough for a test to script exact movements.
    public static SimulationConfig Frozen => Default.WithGravity(100_000);

    public static SimulationConfig WithGravity(this SimulationConfig config, params int[] ticksPerCell)
        => config with { GravityTicksPerCell = ticksPerCell, MaxLevel = ticksPerCell.Length };

    public static SimulationConfig WithLevelUpInterval(this SimulationConfig config, int ticks)
        => config with { LevelUpIntervalTicks = ticks };

    public static SimulationConfig WithLockDelay(this SimulationConfig config, int ticks)
        => config with { LockDelayTicks = ticks };

    public static SimulationConfig WithLockResetMax(this SimulationConfig config, int resets)
        => config with { LockResetMax = resets };
}
