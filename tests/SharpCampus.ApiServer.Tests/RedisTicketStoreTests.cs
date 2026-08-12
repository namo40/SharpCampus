using SharpCampus.ApiServer.Matchmaking;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public class RedisTicketStoreTests
{
    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task StoredTicket_ComesBackWhole()
    {
        var redis = LocalRedis.Connection;
        var store = new RedisTicketStore(redis);

        var userId = new UserId(Guid.NewGuid());
        var ticket = new MatchTicket(new RoomId(Ulid.NewUlid()), "http://localhost:5002", "payload.signature");

        await store.StoreAsync(userId, ticket);

        Assert.Equal(ticket, await store.GetAsync(userId));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task AccountWithoutATicket_HasNone()
    {
        var redis = LocalRedis.Connection;

        Assert.Null(await new RedisTicketStore(redis).GetAsync(new UserId(Guid.NewGuid())));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task StoredTicket_ExpiresWithTheEntryTokenItCarries()
    {
        var redis = LocalRedis.Connection;
        var store = new RedisTicketStore(redis);
        var userId = new UserId(Guid.NewGuid());

        await store.StoreAsync(userId, new MatchTicket(new RoomId(Ulid.NewUlid()), "http://localhost:5002", "token"));

        var lifetime = await redis.GetDatabase().KeyTimeToLiveAsync($"mm:ticket:{userId}");

        Assert.NotNull(lifetime);
        Assert.InRange(lifetime.Value, TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(60));
    }
}
