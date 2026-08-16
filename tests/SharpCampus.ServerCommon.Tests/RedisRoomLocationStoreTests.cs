using SharpCampus.Server.Common.Rooms;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public class RedisRoomLocationStoreTests
{
    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public Task StoredRoom_NamesTheServerThatHoldsIt() => WithRoomAsync(async roomId =>
    {
        var store = new RedisRoomLocationStore(LocalRedis.Connection);

        await store.StoreAsync(roomId, "SharpCampus.RoomServer.1");

        Assert.Equal("SharpCampus.RoomServer.1", await store.FindAsync(roomId));
    });

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public Task RoomNobodyHolds_HasNoServer() => WithRoomAsync(async roomId =>
        Assert.Null(await new RedisRoomLocationStore(LocalRedis.Connection).FindAsync(roomId)));

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public Task ReleasedRoom_IsGoneImmediately() => WithRoomAsync(async roomId =>
    {
        var store = new RedisRoomLocationStore(LocalRedis.Connection);

        await store.StoreAsync(roomId, "SharpCampus.RoomServer.1");
        await store.RemoveAsync(roomId);

        Assert.Null(await store.FindAsync(roomId));
    });

    [Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]
    public Task StoredRoom_CarriesTheLifetimeThatCatchesALostRelease() => WithRoomAsync(async roomId =>
    {
        await new RedisRoomLocationStore(LocalRedis.Connection).StoreAsync(roomId, "SharpCampus.RoomServer.1");

        var lifetime = await LocalRedis.Connection.GetDatabase().KeyTimeToLiveAsync($"room:{roomId}");

        // An entry with no expiry at all would outlive the server that wrote it.
        Assert.NotNull(lifetime);
        Assert.InRange(lifetime.Value, TimeSpan.FromHours(23), TimeSpan.FromHours(24));
    });

    // Every test gets a room id of its own and hands its key back afterwards, whatever the body did.
    private static async Task WithRoomAsync(Func<RoomId, Task> body)
    {
        var roomId = new RoomId(Ulid.NewUlid());

        try
        {
            await body(roomId);
        }
        finally
        {
            await LocalRedis.Connection.GetDatabase().KeyDeleteAsync($"room:{roomId}");
        }
    }
}
