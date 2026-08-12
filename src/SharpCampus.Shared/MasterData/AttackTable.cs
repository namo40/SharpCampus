using MasterMemory;
using MessagePack;

namespace SharpCampus.Shared.MasterData;

/// <summary>
/// How many garbage rows one lock sends, by the number of lines it cleared.
/// </summary>
[MemoryTable("attack_table")]
[MessagePackObject(true)]
public sealed record AttackTable : IValidatable<AttackTable>
{
    // A tetromino spans at most four rows, so a lock can never clear more than four lines.
    /// <summary>Most lines a single lock can clear.</summary>
    public const int MaxLinesCleared = 4;

    /// <summary>Primary key: lines cleared by the lock, from 1 to <see cref="MaxLinesCleared"/>.</summary>
    [PrimaryKey] public int LinesCleared { get; init; }

    /// <summary>Garbage rows sent to the opponent before combo bonuses.</summary>
    public int Garbage { get; init; }

    void IValidatable<AttackTable>.Validate(IValidator<AttackTable> validator)
    {
        validator.Validate(x => x.LinesCleared >= 1);
        validator.Validate(x => x.LinesCleared <= MaxLinesCleared);
        validator.Validate(x => x.Garbage >= 0);

        if (!validator.CallOnce())
        {
            return;
        }

        var rows = validator.GetTableSet().TableData.Count;
        if (rows != MaxLinesCleared)
        {
            validator.Fail($"expected {MaxLinesCleared} rows covering 1..{MaxLinesCleared} cleared lines, found {rows}.");
        }
    }
}
