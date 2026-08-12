using SharpCampus.ApiServer.Matchmaking;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public class RedisPairingLockTests
{
    private const string LockKey = "mm:pair-lock";

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task Lock_IsAcquiredWhenNobodyHoldsIt()
    {
        var pairingLock = await FreshLockAsync();

        try
        {
            Assert.True(await pairingLock.TryAcquireAsync());
        }
        finally
        {
            await ClearAsync();
        }
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task Lock_IsRefusedWhileAnotherInstanceHoldsIt()
    {
        var holder = await FreshLockAsync();
        var contender = new RedisPairingLock(LocalRedis.Connection);

        try
        {
            Assert.True(await holder.TryAcquireAsync());
            Assert.False(await contender.TryAcquireAsync());
        }
        finally
        {
            await ClearAsync();
        }
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task Lock_IsAcquiredAgainOnceItIsReleased()
    {
        var holder = await FreshLockAsync();
        var contender = new RedisPairingLock(LocalRedis.Connection);

        try
        {
            await holder.TryAcquireAsync();
            await holder.ReleaseAsync();

            Assert.True(await contender.TryAcquireAsync());
        }
        finally
        {
            await ClearAsync();
        }
    }

    private static async Task<RedisPairingLock> FreshLockAsync()
    {
        await ClearAsync();
        return new RedisPairingLock(LocalRedis.Connection);
    }

    private static Task ClearAsync() => LocalRedis.Connection.GetDatabase().KeyDeleteAsync(LockKey);
}
