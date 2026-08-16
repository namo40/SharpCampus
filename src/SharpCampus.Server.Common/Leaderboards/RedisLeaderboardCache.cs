using MemoryPack;
using SharpCampus.Shared.Dtos;
using StackExchange.Redis;

namespace SharpCampus.Server.Common.Leaderboards;

public sealed class RedisLeaderboardCache(IConnectionMultiplexer redis) : ILeaderboardCache
{
    // Expiry is the only invalidation there is, so this window is what a settlement can be behind by:
    // for up to five seconds a page still lists the places, and the nicknames, it was assembled from.
    private static readonly TimeSpan _lifetime = TimeSpan.FromSeconds(5);

    public async Task<LeaderboardEntry[]?> GetTopAsync(string board)
    {
        var value = await redis.GetDatabase().StringGetAsync(LeaderboardKeys.Cache(board));
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        var cached = MemoryPackSerializer.Deserialize<CachedLeaderboardEntry[]>(((ReadOnlyMemory<byte>)value).Span);
        if (cached is null)
        {
            return null;
        }

        var entries = new LeaderboardEntry[cached.Length];

        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = new LeaderboardEntry(cached[i].Rank, cached[i].Nickname, cached[i].Score);
        }

        return entries;
    }

    public Task SetTopAsync(string board, LeaderboardEntry[] entries)
    {
        var cached = new CachedLeaderboardEntry[entries.Length];

        for (var i = 0; i < cached.Length; i++)
        {
            cached[i] = new CachedLeaderboardEntry(entries[i].Rank, entries[i].DisplayName, entries[i].Score);
        }

        return redis.GetDatabase()
            .StringSetAsync(LeaderboardKeys.Cache(board), MemoryPackSerializer.Serialize(cached), _lifetime);
    }
}
