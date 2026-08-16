using SharpCampus.Shared.Dtos;

namespace SharpCampus.Server.Common.Leaderboards;

// Assembled pages, nicknames included: a hit answers a ranking query without the board read, the
// nickname query, or the assembly in between.
public interface ILeaderboardCache
{
    // Null when the board has no page cached. A board nobody has reached caches an empty page, and that
    // comes back as an empty one.
    Task<LeaderboardEntry[]?> GetTopAsync(string board);

    Task SetTopAsync(string board, LeaderboardEntry[] entries);
}
