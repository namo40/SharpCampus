using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

// One row per player per game. Only created_at is left to the database; everything else is decided by
// the settlement that inserts the row.
public sealed record MatchRecord(MatchId MatchId, UserId UserId, string Outcome, string EndReason)
{
    public Rating RatingBefore { get; init; }

    public Rating RatingAfter { get; init; }

    public Coins CoinsAwarded { get; init; }

    public int LinesCleared { get; init; }

    public int Quads { get; init; }

    public int GarbageSent { get; init; }

    public int HardDrops { get; init; }

    public int MaxCombo { get; init; }

    public int DurationTicks { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
