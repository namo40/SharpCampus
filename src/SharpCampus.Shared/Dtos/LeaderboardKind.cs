namespace SharpCampus.Shared.Dtos;

/// <summary>
/// Which board a leaderboard call asks for.
/// </summary>
public enum LeaderboardKind : byte
{
    /// <summary>Every account by the rating its settled matches left it on.</summary>
    Rating = 0,

    /// <summary>Wins settled today, by UTC date. Yesterday's board is not kept.</summary>
    DailyWins = 1,
}
