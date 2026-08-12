using MasterMemory;
using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.MasterData;

/// <summary>
/// The single row of match payout and rating constants.
/// </summary>
[MemoryTable("economy")]
[MessagePackObject(true)]
public sealed record Economy : IValidatable<Economy>
{
    /// <summary>Primary key, always <see cref="GameConfig.SingleRowId"/>.</summary>
    [PrimaryKey] public int Id { get; init; }

    /// <summary>Coins the winner of a match is paid.</summary>
    public Coins WinCoins { get; init; }

    /// <summary>Coins the loser of a match is paid.</summary>
    public Coins LoseCoins { get; init; }

    /// <summary>Elo K-factor applied to a rating update.</summary>
    public int RatingK { get; init; }

    /// <summary>Rating a new profile starts at.</summary>
    public Rating RatingInitial { get; init; }

    void IValidatable<Economy>.Validate(IValidator<Economy> validator)
    {
        validator.Validate(x => x.Id == GameConfig.SingleRowId);
        validator.Validate(x => x.WinCoins.AsPrimitive() >= 0);
        validator.Validate(x => x.LoseCoins.AsPrimitive() >= 0);
        validator.Validate(x => x.RatingK > 0);
        validator.Validate(x => x.RatingInitial.AsPrimitive() > 0);

        if (!validator.CallOnce())
        {
            return;
        }

        var rows = validator.GetTableSet().TableData.Count;
        if (rows != 1)
        {
            validator.Fail($"expected exactly one row, found {rows}.");
        }
    }
}
