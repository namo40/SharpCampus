using SharpCampus.Server.Common.Rooms;
using StackExchange.Redis;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public class RedisRoomRegistryTests
{
    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task RegisteredServers_AreRankedByLoad()
    {
        var redis = LocalRedis.Connection;
        var registry = await FreshRegistryAsync(redis);

        await registry.RegisterAsync(new RoomServerEntry("busy", "http://busy:5002", "http://busy:5002", 7, 10));
        await registry.RegisterAsync(new RoomServerEntry("idle", "http://idle:5002", "http://idle:5002", 1, 10));

        var chosen = await registry.FindLeastLoadedAsync();

        Assert.NotNull(chosen);
        Assert.Equal("idle", chosen.Name);
        Assert.Equal("http://idle:5002", chosen.ClientEndpoint);
        Assert.Equal(1, chosen.RoomCount);
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task ServerAtCapacity_IsNotHandedOut()
    {
        var redis = LocalRedis.Connection;
        var registry = await FreshRegistryAsync(redis);

        await registry.RegisterAsync(new RoomServerEntry("full", "http://full:5002", "http://full:5002", 10, 10));

        Assert.Null(await registry.FindLeastLoadedAsync());
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task RemovedServer_IsGoneImmediately()
    {
        var redis = LocalRedis.Connection;
        var registry = await FreshRegistryAsync(redis);

        await registry.RegisterAsync(new RoomServerEntry("one", "http://one:5002", "http://one:5002", 0, 10));
        await registry.RemoveAsync("one");

        Assert.Null(await registry.FindLeastLoadedAsync());
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task NameWhoseEntryExpired_IsPrunedOnLookup()
    {
        var redis = LocalRedis.Connection;
        var registry = await FreshRegistryAsync(redis);
        var database = redis.GetDatabase();

        await registry.RegisterAsync(new RoomServerEntry("gone", "http://gone:5002", "http://gone:5002", 0, 10));

        // Stands in for a server that stopped sending heartbeats and let its entry expire.
        await database.KeyDeleteAsync("rs:gone");

        Assert.Null(await registry.FindLeastLoadedAsync());
        Assert.Equal(0, await database.SetLengthAsync("rs:names"));
    }

    private static async Task<RedisRoomRegistry> FreshRegistryAsync(IConnectionMultiplexer redis)
    {
        var database = redis.GetDatabase();

        foreach (var member in await database.SetMembersAsync("rs:names"))
        {
            await database.KeyDeleteAsync($"rs:{member}");
        }

        await database.KeyDeleteAsync("rs:names");
        return new RedisRoomRegistry(redis);
    }
}
