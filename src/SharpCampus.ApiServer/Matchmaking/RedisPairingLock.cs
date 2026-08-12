using StackExchange.Redis;

namespace SharpCampus.ApiServer.Matchmaking;

// SET NX with an expiry: whoever writes the key first pairs, and the expiry hands the work on if that
// instance dies before releasing it.
public sealed class RedisPairingLock(IConnectionMultiplexer redis) : IPairingLock
{
    private const string LockKey = "mm:pair-lock";
    private static readonly TimeSpan _lifetime = TimeSpan.FromSeconds(5);

    public Task<bool> TryAcquireAsync()
        => redis.GetDatabase().StringSetAsync(LockKey, Environment.MachineName, _lifetime, When.NotExists);

    // Production note: a holder that overran the expiry deletes a lock somebody else now owns. A real
    // one would delete only its own token, which takes the compare-and-delete script this avoids.
    public Task ReleaseAsync() => redis.GetDatabase().KeyDeleteAsync(LockKey);
}
