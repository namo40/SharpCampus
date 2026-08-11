namespace SharpCampus.GameCore;

// Balance data is injected: the simulation deliberately ships no defaults of its own.
public readonly record struct SimulationConfig(
    int LockDelayTicks,
    int LockResetMax,
    int NextCount,
    int GarbageCapPerLock,
    IReadOnlyList<int> GravityTicksPerCell,
    int LevelUpIntervalTicks,
    int MaxLevel,
    IReadOnlyList<int> AttackByLines,
    IReadOnlyList<ComboBonusRange> ComboBonus)
{
    public int LevelAt(int elapsedTicks) => Math.Min(MaxLevel, 1 + (elapsedTicks / LevelUpIntervalTicks));

    public int GravityIntervalTicks(int level)
        => GravityTicksPerCell[Math.Min(level, GravityTicksPerCell.Count) - 1];

    public int AttackFor(int lines, int combo) => AttackByLines[lines - 1] + ComboBonusFor(combo);

    public int ComboBonusFor(int combo)
    {
        for (var i = 0; i < ComboBonus.Count; i++)
        {
            var range = ComboBonus[i];
            if (combo >= range.Min && combo <= range.Max)
            {
                return range.Bonus;
            }
        }

        return 0;
    }
}
