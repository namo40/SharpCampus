using SharpCampus.Server.Common.Leaderboards;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public class RedisLeaderboardStoreTests
{
    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task Rating_ReflectsTheLatestSettlementInBothDirections()
    {
        var store = new RedisLeaderboardStore(LocalRedis.Connection);
        var userId = new UserId(Guid.NewGuid());

        try
        {
            await store.SetRatingAsync(userId, new Rating(1200));
            await store.SetRatingAsync(userId, new Rating(1000));

            // A lost match lowers the stored score, which a greater-than write would have kept at 1200.
            Assert.Equal(1000, (await store.FindAsync(LeaderboardKeys.Rating, userId))?.Score);
        }
        finally
        {
            // The rating board is the one shared key, so the test takes its member back out.
            await LocalRedis.Connection.GetDatabase()
                .SortedSetRemoveAsync(LeaderboardKeys.Rating, userId.ToString());
        }
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task AccountTheBoardHasNeverSeen_HasNoPlace()
    {
        var store = new RedisLeaderboardStore(LocalRedis.Connection);

        Assert.Null(await store.FindAsync(LeaderboardKeys.Rating, new UserId(Guid.NewGuid())));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task DailyWins_AccumulateIntoPlacesBestFirst()
    {
        var store = new RedisLeaderboardStore(LocalRedis.Connection);
        var date = FarOffDay();
        var board = LeaderboardKeys.Daily(date);
        var leader = new UserId(Guid.NewGuid());
        var runnerUp = new UserId(Guid.NewGuid());

        await store.AddDailyWinAsync(leader, date);
        await store.AddDailyWinAsync(runnerUp, date);
        await store.AddDailyWinAsync(leader, date);

        var top = await store.TopAsync(board, 10);

        Assert.Equal([(leader, 0L, 2), (runnerUp, 1L, 1)], top.Select(row => (row.UserId, row.Rank, row.Score)));
        Assert.Equal(1, (await store.FindAsync(board, runnerUp))?.Rank);
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task DailyBoard_IsBornWithTheLifetimeThatEndsIt()
    {
        var store = new RedisLeaderboardStore(LocalRedis.Connection);
        var date = FarOffDay();
        var userId = new UserId(Guid.NewGuid());

        await store.AddDailyWinAsync(userId, date);
        var first = await LocalRedis.Connection.GetDatabase().KeyTimeToLiveAsync(LeaderboardKeys.Daily(date));

        await store.AddDailyWinAsync(userId, date);
        var second = await LocalRedis.Connection.GetDatabase().KeyTimeToLiveAsync(LeaderboardKeys.Daily(date));

        Assert.NotNull(first);
        Assert.InRange(first.Value, TimeSpan.Zero, TimeSpan.FromHours(48));

        // The second win must not hand the board a fresh lifetime, or a busy day would never expire.
        Assert.NotNull(second);
        Assert.True(second <= first);
    }

    // A day no real settlement will ever stamp, so the board under test is this test's alone. Its key
    // carries the usual lifetime and cleans itself up.
    private static DateOnly FarOffDay() => new DateOnly(3000, 1, 1).AddDays(Random.Shared.Next(365_000));
}
