using MagicOnion;
using SharpCampus.Shared.Dtos;

namespace SharpCampus.Shared.Services;

/// <summary>
/// The boards settled matches feed. Every method requires a bearer token in the <c>authorization</c>
/// request header.
/// </summary>
public interface ILeaderboardService : IService<ILeaderboardService>
{
    /// <summary>
    /// Reads a board's leading places together with the caller's own place on it.
    /// <see cref="LeaderboardKind.DailyWins"/> counts the wins settled on today's UTC date.
    /// </summary>
    /// <param name="kind">Board to read.</param>
    /// <returns>The first 100 places, and the caller's place when the caller is on the board.</returns>
    UnaryResult<LeaderboardView> GetLeaderboardAsync(LeaderboardKind kind);
}
