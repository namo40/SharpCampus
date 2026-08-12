using MasterMemory;
using MessagePack;

namespace SharpCampus.Shared.MasterData;

/// <summary>
/// Extra garbage granted for consecutive clears, as one row per combo range.
/// </summary>
[MemoryTable("combo_table")]
[MessagePackObject(true)]
public sealed record ComboTable : IValidatable<ComboTable>
{
    /// <summary>Primary key: lowest combo count this range covers.</summary>
    [PrimaryKey] public int ComboMin { get; init; }

    /// <summary>Highest combo count this range covers.</summary>
    public int ComboMax { get; init; }

    /// <summary>Garbage rows added on top of the attack table result.</summary>
    public int Bonus { get; init; }

    void IValidatable<ComboTable>.Validate(IValidator<ComboTable> validator)
    {
        validator.Validate(x => x.ComboMin >= 1);
        validator.Validate(x => x.ComboMax >= x.ComboMin);
        validator.Validate(x => x.Bonus >= 1);

        if (!validator.CallOnce())
        {
            return;
        }

        // A combo count must land in at most one range: the attack lookup takes the first match and stops.
        var ranges = validator.GetTableSet().TableData.OrderBy(x => x.ComboMin).ToArray();
        for (var i = 1; i < ranges.Length; i++)
        {
            var previous = ranges[i - 1];
            var current = ranges[i];
            if (current.ComboMin <= previous.ComboMax)
            {
                validator.Fail($"range {current.ComboMin}..{current.ComboMax} overlaps {previous.ComboMin}..{previous.ComboMax}.");
            }
        }
    }
}
