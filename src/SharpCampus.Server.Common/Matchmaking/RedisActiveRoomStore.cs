using System.Text.Json;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using StackExchange.Redis;

namespace SharpCampus.Server.Common.Matchmaking;

// The entry the reconnect path reads: rerunning the duel command finds the room the client fell out
// of instead of queueing for a new one.
public sealed class RedisActiveRoomStore(IConnectionMultiplexer redis) : IActiveRoomStore
{
    // Production note: the room deletes its own entry, so this lifetime only catches entries left by
    // a room server that died. A live service would tie it to the registry heartbeat instead.
    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(1);

    public async Task<ActiveRoom?> GetAsync(UserId userId)
    {
        var value = await redis.GetDatabase().StringGetAsync(MatchmakingKeys.ActiveRoom(userId));
        if (value.IsNull)
        {
            return null;
        }

        var stored = JsonSerializer.Deserialize<StoredRoom>(value.ToString(), JsonSerializerOptions.Web)!;
        return new ActiveRoom(RoomId.Parse(stored.RoomId), stored.Endpoint);
    }

    public Task StoreAsync(UserId userId, ActiveRoom room)
    {
        var stored = new StoredRoom(room.RoomId.ToString(), room.Endpoint);

        return redis.GetDatabase().StringSetAsync(
            MatchmakingKeys.ActiveRoom(userId),
            JsonSerializer.Serialize(stored, JsonSerializerOptions.Web),
            _lifetime);
    }

    public Task ReleaseAsync(UserId userId) => redis.GetDatabase().KeyDeleteAsync([
        (RedisKey)MatchmakingKeys.ActiveRoom(userId),
        (RedisKey)MatchmakingKeys.Ticket(userId),
    ]);

    // What sits in Redis is plain JSON, so a match can be followed from redis-cli while it runs.
    private sealed record StoredRoom(string RoomId, string Endpoint);
}
