using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using StackExchange.Redis;

namespace SharpCampus.Server.Common.Leaderboards;

public sealed class RedisLeaderboardStore(IConnectionMultiplexer redis) : ILeaderboardStore
{
    private static readonly TimeSpan _dailyLifetime = TimeSpan.FromHours(48);

    // Plain ZADD rather than a greater-than update: a rating moves both ways, so the newest value wins.
    public Task SetRatingAsync(UserId userId, Rating rating) => redis.GetDatabase()
        .SortedSetAddAsync(LeaderboardKeys.Rating, userId.ToString(), rating.AsPrimitive());

    public async Task AddDailyWinAsync(UserId userId, DateOnly date)
    {
        var database = redis.GetDatabase();
        var key = LeaderboardKeys.Daily(date);

        await database.SortedSetIncrementAsync(key, userId.ToString(), 1);

        // Set once, on the increment that created the key: yesterday's board is never read again, it only
        // has to die on its own.
        await database.KeyExpireAsync(key, _dailyLifetime, ExpireWhen.HasNoExpiry);
    }

    public async Task<LeaderboardRow[]> TopAsync(string board, int count)
    {
        var entries = await redis.GetDatabase()
            .SortedSetRangeByRankWithScoresAsync(board, 0, count - 1, Order.Descending);

        var rows = new LeaderboardRow[entries.Length];

        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = new LeaderboardRow(UserId.Parse(entries[i].Element.ToString()), i, (int)entries[i].Score);
        }

        return rows;
    }

    public async Task<LeaderboardRow?> FindAsync(string board, UserId userId)
    {
        var database = redis.GetDatabase();
        var member = userId.ToString();

        // Equal scores are ordered by member, so an account's place among the players it is tied with is
        // decided by its identifier.
        if (await database.SortedSetRankAsync(board, member, Order.Descending) is not { } rank)
        {
            return null;
        }

        var score = await database.SortedSetScoreAsync(board, member) ?? 0;

        return new LeaderboardRow(userId, rank, (int)score);
    }
}
