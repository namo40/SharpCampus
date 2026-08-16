using SharpCampus.ApiServer.Matchmaking;
using SharpCampus.Shared.Identity;
using StackExchange.Redis;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public class RedisMatchQueueTests : IAsyncLifetime
{
    private static readonly UserId _first = new(Guid.NewGuid());
    private static readonly UserId _second = new(Guid.NewGuid());
    private static readonly UserId _third = new(Guid.NewGuid());

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // These tests share the developer's live Redis with the real servers. An entry a test leaves in
    // the queue greets the next server run as a ghost account that never joins its match, so every
    // test hands the keys back empty.
    public async ValueTask DisposeAsync()
    {
        if (LocalRedis.IsRunning)
        {
            await LocalRedis.Connection.GetDatabase().KeyDeleteAsync(["mm:queue", "mm:waiting-since"]);
        }
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task Pair_IsTheTwoWhoHaveWaitedLongest()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        await queue.EnqueueAsync(_first);
        await queue.EnqueueAsync(_second);
        await queue.EnqueueAsync(_third);

        Assert.Equal((_first, _second), await queue.PeekPairAsync());
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task Pair_IsEmptyUntilTwoAreWaiting()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        Assert.Null(await queue.PeekPairAsync());

        await queue.EnqueueAsync(_first);

        Assert.Null(await queue.PeekPairAsync());
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task Peek_LeavesBothPlayersInTheQueue()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        await queue.EnqueueAsync(_first);
        await queue.EnqueueAsync(_second);

        Assert.Equal(await queue.PeekPairAsync(), await queue.PeekPairAsync());
        Assert.True(await queue.ContainsAsync(_first));
        Assert.True(await queue.ContainsAsync(_second));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task ConfirmedPair_LeavesTheQueueAndTheNextPairMovesUp()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        await queue.EnqueueAsync(_first);
        await queue.EnqueueAsync(_second);
        await queue.EnqueueAsync(_third);

        await queue.RemoveAsync(_first);
        await queue.RemoveAsync(_second);

        Assert.False(await queue.ContainsAsync(_first));
        Assert.True(await queue.ContainsAsync(_third));
        Assert.Null(await queue.PeekPairAsync());
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task EnqueueingTwice_DoesNotQueueTheSameAccountTwice()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        Assert.True(await queue.EnqueueAsync(_first));
        Assert.False(await queue.EnqueueAsync(_first));

        await queue.EnqueueAsync(_second);

        Assert.Equal((_first, _second), await queue.PeekPairAsync());
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task CancelledPlayer_IsNoLongerInTheQueue()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        await queue.EnqueueAsync(_first);
        await queue.RemoveAsync(_first);

        Assert.False(await queue.ContainsAsync(_first));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task LoneEntry_CarriesHowLongItHasBeenWaiting()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        Assert.Null(await queue.PeekLoneAsync());

        await queue.EnqueueAsync(_first);
        var lone = await queue.PeekLoneAsync();

        Assert.NotNull(lone);
        Assert.Equal(_first, lone.Value.UserId);
        Assert.InRange(lone.Value.Waited, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task LoneEntry_IsNobodyWhileTwoStillHaveEachOther()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        await queue.EnqueueAsync(_first);
        await queue.EnqueueAsync(_second);

        Assert.Null(await queue.PeekLoneAsync());
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task EntryWithNoTimestamp_ReadsAsInfinitelyOld()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        // What a queue written before the timestamps existed looks like. Reading it as ancient is what
        // flushes it out through the bot path rather than leaving it there for good.
        await redis.GetDatabase().ListLeftPushAsync("mm:queue", _third.ToString());

        var lone = await queue.PeekLoneAsync();

        Assert.NotNull(lone);
        Assert.True(lone.Value.Waited > TimeSpan.FromDays(365));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task CancelledPlayer_TakesTheirWaitingTimeWithThem()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        await queue.EnqueueAsync(_first);
        await queue.RemoveAsync(_first);
        await queue.EnqueueAsync(_second);

        Assert.False(await redis.GetDatabase().HashExistsAsync("mm:waiting-since", _first.ToString()));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task HandWrittenJunk_IsDroppedInsteadOfStoppingThePairingWorker()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        await redis.GetDatabase().ListLeftPushAsync("mm:queue", "not-an-account-id");

        Assert.Null(await queue.PeekLoneAsync());
        Assert.Equal(0, await redis.GetDatabase().ListLengthAsync("mm:queue"));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task JunkBesideARealPlayer_LeavesThePlayerWaitingForTheNextPass()
    {
        var redis = LocalRedis.Connection;
        var queue = await FreshQueueAsync(redis);

        await queue.EnqueueAsync(_first);
        await redis.GetDatabase().ListLeftPushAsync("mm:queue", "");

        Assert.Null(await queue.PeekPairAsync());

        await queue.EnqueueAsync(_second);

        Assert.Equal((_first, _second), await queue.PeekPairAsync());
    }

    private static async Task<RedisMatchQueue> FreshQueueAsync(IConnectionMultiplexer redis)
    {
        await redis.GetDatabase().KeyDeleteAsync("mm:queue");
        await redis.GetDatabase().KeyDeleteAsync("mm:waiting-since");
        return new RedisMatchQueue(redis, TimeProvider.System);
    }
}
