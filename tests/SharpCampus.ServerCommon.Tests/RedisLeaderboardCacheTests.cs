using SharpCampus.Server.Common.Leaderboards;
using SharpCampus.Shared.Dtos;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public class RedisLeaderboardCacheTests
{
    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public Task StoredPage_ComesBackAsTheOneThatWentIn() => WithBoardAsync(async board =>
    {
        var cache = new RedisLeaderboardCache(LocalRedis.Connection);
        LeaderboardEntry[] page = [new(1, "alice", 1500), new(2, "bob", 1400), new(3, "carol", 1399)];

        await cache.SetTopAsync(board, page);

        Assert.Equal(page, await cache.GetTopAsync(board));
    });

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public Task NicknamesAndNumbers_SurviveTheSerializerUnchanged() => WithBoardAsync(async board =>
    {
        var cache = new RedisLeaderboardCache(LocalRedis.Connection);

        // Nicknames are the reason the page is cached assembled, so the ones that stress a byte-oriented
        // format are the ones worth reading back: outside ASCII, and empty.
        LeaderboardEntry[] page =
        [
            new(1, "테트로미노", int.MaxValue),
            new(2, "プレイヤー・二", 0),
            new(3, "", int.MinValue),
        ];

        await cache.SetTopAsync(board, page);
        var cached = await cache.GetTopAsync(board);

        Assert.NotNull(cached);
        Assert.Equal(page, cached);
    });

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public Task BoardWithNothingCached_AnswersWithNothing() => WithBoardAsync(async board =>
        Assert.Null(await new RedisLeaderboardCache(LocalRedis.Connection).GetTopAsync(board)));

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public Task PageOfNoPlaces_IsAHitAndNotAMiss() => WithBoardAsync(async board =>
    {
        var cache = new RedisLeaderboardCache(LocalRedis.Connection);

        // A board nobody has reached is worth caching too, or every call to it would assemble nothing
        // the slow way.
        await cache.SetTopAsync(board, []);

        Assert.Empty(Assert.IsType<LeaderboardEntry[]>(await cache.GetTopAsync(board)));
    });

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public Task CachedPage_IsBornWithTheLifetimeThatEndsIt() => WithBoardAsync(async board =>
    {
        var cache = new RedisLeaderboardCache(LocalRedis.Connection);

        await cache.SetTopAsync(board, [new LeaderboardEntry(1, "alice", 1500)]);
        var lifetime = await LocalRedis.Connection.GetDatabase().KeyTimeToLiveAsync(LeaderboardKeys.Cache(board));

        // Expiry is the only invalidation, so a page that outlived this window would be a page nothing
        // ever takes back.
        Assert.NotNull(lifetime);
        Assert.InRange(lifetime.Value, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    });

    // Every test gets a board name of its own and hands its key back afterwards, whatever the body did:
    // a leftover cached page would be read by a later run as a board that was never settled.
    private static async Task WithBoardAsync(Func<string, Task> body)
    {
        var board = $"lb:test:{Guid.NewGuid():N}";

        try
        {
            await body(board);
        }
        finally
        {
            await LocalRedis.Connection.GetDatabase().KeyDeleteAsync(LeaderboardKeys.Cache(board));
        }
    }
}
