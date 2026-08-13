using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public class RedisActiveRoomStoreTests
{
    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task StoredRoom_ComesBackWhole()
    {
        var store = new RedisActiveRoomStore(LocalRedis.Connection);
        var userId = new UserId(Guid.NewGuid());
        var room = new ActiveRoom(new RoomId(Ulid.NewUlid()), "http://localhost:5002");

        await store.StoreAsync(userId, room);

        Assert.Equal(room, await store.GetAsync(userId));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task AccountThatIsNotPlaying_HasNoRoom()
    {
        var store = new RedisActiveRoomStore(LocalRedis.Connection);

        Assert.Null(await store.GetAsync(new UserId(Guid.NewGuid())));
    }

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public async Task ReleasingARoom_TakesThePairingTicketWithIt()
    {
        var redis = LocalRedis.Connection;
        var store = new RedisActiveRoomStore(redis);
        var userId = new UserId(Guid.NewGuid());

        await store.StoreAsync(userId, new ActiveRoom(new RoomId(Ulid.NewUlid()), "http://localhost:5002"));
        await redis.GetDatabase().StringSetAsync(MatchmakingKeys.Ticket(userId), "{}");

        await store.ReleaseAsync(userId);

        // A ticket that outlived its room is what turns the next queue attempt into a refused seat.
        Assert.Null(await store.GetAsync(userId));
        Assert.False(await redis.GetDatabase().KeyExistsAsync(MatchmakingKeys.Ticket(userId)));
    }
}
