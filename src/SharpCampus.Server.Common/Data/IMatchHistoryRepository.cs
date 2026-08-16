using SharpCampus.Shared.Identity;

namespace SharpCampus.Server.Common.Data;

public interface IMatchHistoryRepository
{
    // How many rows come back is the caller's choice, because the cap belongs to the contract being served
    // rather than to the table.
    Task<IReadOnlyList<MatchHistoryRow>> GetRecentAsync(UserId userId, int count);
}

// A game as one row: what the player asking for it did, and who was on the other side. The opponent is
// only an id here, since a history table is stored per player and knows no names.
public sealed record MatchHistoryRow(MatchRecord Match, UserId? OpponentId);
