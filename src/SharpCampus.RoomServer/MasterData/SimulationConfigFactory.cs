using SharpCampus.GameCore;
using SharpCampus.Shared.MasterData;

namespace SharpCampus.RoomServer.MasterData;

// The simulation takes no dependency on master data, so this is the one place where balance rows
// become simulation constants. Seconds live only in the source data; everything past here is ticks.
public static class SimulationConfigFactory
{
    public static DuelRules Create(MemoryDatabase database)
    {
        var config = database.GameConfigTable.All[0];

        // MasterMemory keeps each table sorted by its primary key, which is the level here and the
        // cleared line count below, so both project straight onto a zero-based lookup.
        var gravity = new int[database.GravityCurveTable.Count];
        for (var i = 0; i < gravity.Length; i++)
        {
            gravity[i] = database.GravityCurveTable.All[i].TicksPerCell;
        }

        var attack = new int[database.AttackTableTable.Count];
        for (var i = 0; i < attack.Length; i++)
        {
            attack[i] = database.AttackTableTable.All[i].Garbage;
        }

        var combo = new ComboBonusRange[database.ComboTableTable.Count];
        for (var i = 0; i < combo.Length; i++)
        {
            var row = database.ComboTableTable.All[i];
            combo[i] = new ComboBonusRange(row.ComboMin, row.ComboMax, row.Bonus);
        }

        var simulation = new SimulationConfig(
            LockDelayTicks: config.LockDelayTicks,
            LockResetMax: config.LockResetMax,
            NextCount: config.NextCount,
            GarbageCapPerLock: config.GarbageCapPerLock,
            GravityTicksPerCell: gravity,
            LevelUpIntervalTicks: config.LevelUpIntervalSec * config.TickRate,
            MaxLevel: config.MaxLevel,
            AttackByLines: attack,
            ComboBonus: combo);

        return new DuelRules(
            simulation,
            config.TickRate,
            config.InputPerTickMax,
            config.NextCount,
            config.CountdownSec * config.TickRate,
            config.JoinTimeoutSec * config.TickRate,
            config.GraceTicks,
            config.RematchTimeoutSec * config.TickRate);
    }
}
