using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Leaderboards;

// A view of what the settlement already committed, never a source of truth: a push that never lands
// leaves the board behind, and nothing about the match changes because of it.
public interface ILeaderboardStore
{
    Task SetRatingAsync(UserId userId, Rating rating);

    Task AddDailyWinAsync(UserId userId, DateOnly date);

    Task<LeaderboardRow[]> TopAsync(string board, int count);

    // Null when the account has never been written to this board.
    Task<LeaderboardRow?> FindAsync(string board, UserId userId);
}

// Rank counts from zero, the way Redis reports it.
public sealed record LeaderboardRow(UserId UserId, long Rank, int Score);
