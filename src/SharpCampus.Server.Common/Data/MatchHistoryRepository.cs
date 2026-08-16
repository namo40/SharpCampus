using Microsoft.EntityFrameworkCore;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

public sealed class MatchHistoryRepository(SharpCampusDbContext db) : IMatchHistoryRepository
{
    public async Task<IReadOnlyList<MatchHistoryRow>> GetRecentAsync(UserId userId, int count)
    {
        var mine = await db.MatchRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId)
            .OrderByDescending(record => record.CreatedAt)
            // Two games settled inside the same timestamp would otherwise come back in whatever order the
            // scan produced, which is not a page a client can trust. The id is a ULID, so it breaks the tie
            // by the moment the room issued it.
            .ThenByDescending(record => record.MatchId)
            .Take(count)
            .ToListAsync();

        if (mine.Count == 0)
        {
            return [];
        }

        var matchIds = mine.Select(record => record.MatchId).ToList();

        // The opponent is the other row of the same game, so the whole page is answered by one more query
        // instead of one per game.
        var opponents = await db.MatchRecords
            .AsNoTracking()
            .Where(record => matchIds.Contains(record.MatchId) && record.UserId != userId)
            .Select(record => new { record.MatchId, record.UserId })
            .ToDictionaryAsync(row => row.MatchId, row => row.UserId);

        return [.. mine.Select(record => new MatchHistoryRow(record, OpponentOf(opponents, record.MatchId)))];
    }

    private static UserId? OpponentOf(IReadOnlyDictionary<MatchId, UserId> opponents, MatchId matchId) =>
        opponents.TryGetValue(matchId, out var opponent) ? opponent : null;
}
