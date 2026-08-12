using SharpCampus.ApiServer.Matchmaking;
using SharpCampus.Shared.Identity;
using StackExchange.Redis;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public class RedisMatchQueueTests
{
    private static readonly UserId _first = new(Guid.NewGuid());
    private static readonly UserId _second = new(Guid.NewGuid());
    private static readonly UserId _third = new(Guid.NewGuid());

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

    private static async Task<RedisMatchQueue> FreshQueueAsync(IConnectionMultiplexer redis)
    {
        await redis.GetDatabase().KeyDeleteAsync("mm:queue");
        return new RedisMatchQueue(redis);
    }
}
