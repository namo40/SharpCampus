using SharpCampus.Shared.Identity;
using StackExchange.Redis;

namespace SharpCampus.ApiServer.Matchmaking;

// A plain Redis list used as a FIFO: new arrivals go on the left, pairing reads from the right. Living
// in Redis rather than process memory is what lets several ApiServer instances share one queue.
public sealed class RedisMatchQueue(IConnectionMultiplexer redis) : IMatchQueue
{
    private const string QueueKey = "mm:queue";

    public async Task<bool> EnqueueAsync(UserId userId)
    {
        var database = redis.GetDatabase();
        if (await database.ListPositionAsync(QueueKey, userId.ToString()) >= 0)
        {
            return false;
        }

        await database.ListLeftPushAsync(QueueKey, userId.ToString());
        return true;
    }

    public async Task<bool> ContainsAsync(UserId userId)
        => await redis.GetDatabase().ListPositionAsync(QueueKey, userId.ToString()) >= 0;

    public Task RemoveAsync(UserId userId)
        => redis.GetDatabase().ListRemoveAsync(QueueKey, userId.ToString());

    public async Task<(UserId First, UserId Second)?> PeekPairAsync()
    {
        // The last two entries, in list order. Arrivals are pushed on the left, so the very last of
        // them is the one that has waited longest.
        var waiting = await redis.GetDatabase().ListRangeAsync(QueueKey, -2);

        return waiting.Length < 2
            ? null
            : (UserId.Parse(waiting[1].ToString()), UserId.Parse(waiting[0].ToString()));
    }
}
