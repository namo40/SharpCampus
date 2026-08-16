using SharpCampus.Shared.Identity;
using StackExchange.Redis;

namespace SharpCampus.ApiServer.Matchmaking;

// A plain Redis list used as a FIFO: new arrivals go on the left, pairing reads from the right. Living
// in Redis rather than process memory is what lets several ApiServer instances share one queue.
public sealed class RedisMatchQueue(IConnectionMultiplexer redis, TimeProvider time) : IMatchQueue
{
    private const string QueueKey = "mm:queue";

    // How long somebody has been waiting cannot be read off the list, and it is what decides whether a
    // bot is sent for. Kept beside the list so its entries stay plain account ids.
    private const string WaitingSinceKey = "mm:waiting-since";

    public async Task<bool> EnqueueAsync(UserId userId)
    {
        var database = redis.GetDatabase();
        if (await database.ListPositionAsync(QueueKey, userId.ToString()) >= 0)
        {
            return false;
        }

        await database.ListLeftPushAsync(QueueKey, userId.ToString());
        await database.HashSetAsync(WaitingSinceKey, userId.ToString(), time.GetUtcNow().ToUnixTimeMilliseconds());
        return true;
    }

    public async Task<bool> ContainsAsync(UserId userId)
        => await redis.GetDatabase().ListPositionAsync(QueueKey, userId.ToString()) >= 0;

    public async Task RemoveAsync(UserId userId)
    {
        var database = redis.GetDatabase();
        await database.ListRemoveAsync(QueueKey, userId.ToString());
        await database.HashDeleteAsync(WaitingSinceKey, userId.ToString());
    }

    public async Task<(UserId First, UserId Second)?> PeekPairAsync()
    {
        var database = redis.GetDatabase();

        // The last two entries, in list order. Arrivals are pushed on the left, so the very last of
        // them is the one that has waited longest.
        var waiting = await database.ListRangeAsync(QueueKey, -2);

        return waiting.Length < 2
            || await ReadAsync(database, waiting[1]) is not { } first
            || await ReadAsync(database, waiting[0]) is not { } second
            ? null
            : (first, second);
    }

    public async Task<(UserId UserId, TimeSpan Waited)?> PeekLoneAsync()
    {
        var database = redis.GetDatabase();

        // Two entries are a pair the caller has already had its chance at, so only a list of exactly
        // one has nobody left to be matched with.
        var waiting = await database.ListRangeAsync(QueueKey, 0, 1);
        if (waiting.Length != 1 || await ReadAsync(database, waiting[0]) is not { } userId)
        {
            return null;
        }

        var since = await database.HashGetAsync(WaitingSinceKey, waiting[0]);

        // An entry left over from before this key existed has no timestamp, and reading it as
        // infinitely old is what clears it out: it draws a bot, the account it belongs to never turns
        // up, and the room that times out on it takes the queue entry with it.
        var waitingSince = since.TryParse(out long milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : DateTimeOffset.UnixEpoch;

        return (userId, time.GetUtcNow() - waitingSince);
    }

    // Enqueue only ever stores account ids, so an entry that is not one was written into Redis by
    // hand. Parsing it would take the pairing worker down with the whole host (a BackgroundService
    // failure stops it), so the junk is dropped instead and the pass carries on.
    private static async Task<UserId?> ReadAsync(IDatabase database, RedisValue entry)
    {
        if (UserId.TryParse(entry.ToString(), out var userId))
        {
            return userId;
        }

        await database.ListRemoveAsync(QueueKey, entry);
        await database.HashDeleteAsync(WaitingSinceKey, entry);
        return null;
    }
}
