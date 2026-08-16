using SharpCampus.Shared.Values;
using StackExchange.Redis;

namespace SharpCampus.Server.Common.Rooms;

// One string per live room, naming the registered server that owns it.
public sealed class RedisRoomLocationStore(IConnectionMultiplexer redis) : IRoomLocationStore
{
    // The delete on release is the real lifecycle; this only stops a leak if that delete never lands.
    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(24);

    public Task StoreAsync(RoomId roomId, string serverName) =>
        redis.GetDatabase().StringSetAsync(LocationKey(roomId), serverName, _lifetime);

    public Task RemoveAsync(RoomId roomId) => redis.GetDatabase().KeyDeleteAsync(LocationKey(roomId));

    public async Task<string?> FindAsync(RoomId roomId)
    {
        var value = await redis.GetDatabase().StringGetAsync(LocationKey(roomId));
        return value.IsNull ? null : value.ToString();
    }

    private static string LocationKey(RoomId roomId) => $"room:{roomId}";
}
