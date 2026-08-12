using MasterMemory;
using MessagePack;

namespace SharpCampus.Shared.MasterData;

/// <summary>
/// How fast a piece falls at one level, as the number of ticks it spends on each cell.
/// </summary>
[MemoryTable("gravity_curve")]
[MessagePackObject(true)]
public sealed record GravityCurve : IValidatable<GravityCurve>
{
    /// <summary>Primary key: the level this row applies to.</summary>
    [PrimaryKey] public int Level { get; init; }

    /// <summary>Ticks a piece takes to fall one cell at this level.</summary>
    public int TicksPerCell { get; init; }

    void IValidatable<GravityCurve>.Validate(IValidator<GravityCurve> validator)
    {
        validator.Validate(x => x.Level >= 1);
        validator.Validate(x => x.TicksPerCell >= 1);

        if (!validator.CallOnce())
        {
            return;
        }

        // The tick loop indexes this curve by the level it derives from elapsed time, so a gap below
        // GameConfig.MaxLevel would be an out-of-range read at the worst possible moment.
        var maxLevel = validator.GetReferenceSet<GameConfig>().TableData.FirstOrDefault()?.MaxLevel ?? 0;
        var levels = validator.GetTableSet().TableData.Select(x => x.Level).ToHashSet();

        for (var level = 1; level <= maxLevel; level++)
        {
            if (!levels.Contains(level))
            {
                validator.Fail($"level {level} is missing, but game_config.MaxLevel is {maxLevel}.");
            }
        }
    }
}
