using StackExchange.Redis;
using ZLinq;

namespace SharpCampus.Server.Common.Rooms;

// A set of names plus one hash per server. The hash carries a TTL the heartbeat keeps refreshing, so a
// server that dies drops out of the registry on its own without anyone having to notice.
public sealed class RedisRoomRegistry(IConnectionMultiplexer redis) : IRoomRegistry
{
    private const string NamesKey = "rs:names";
    private static readonly TimeSpan _entryLifetime = TimeSpan.FromSeconds(15);

    private const string ClientEndpointField = "clientEndpoint";
    private const string ControlEndpointField = "controlEndpoint";
    private const string RoomCountField = "roomCount";
    private const string CapacityField = "capacity";

    public async Task RegisterAsync(RoomServerEntry entry)
    {
        var database = redis.GetDatabase();
        var key = EntryKey(entry.Name);

        await database.HashSetAsync(key, [
            new HashEntry(ClientEndpointField, entry.ClientEndpoint),
            new HashEntry(ControlEndpointField, entry.ControlEndpoint),
            new HashEntry(RoomCountField, entry.RoomCount),
            new HashEntry(CapacityField, entry.Capacity),
        ]);
        await database.KeyExpireAsync(key, _entryLifetime);
        await database.SetAddAsync(NamesKey, entry.Name);
    }

    public async Task RemoveAsync(string name)
    {
        var database = redis.GetDatabase();
        await database.KeyDeleteAsync(EntryKey(name));
        await database.SetRemoveAsync(NamesKey, name);
    }

    public async Task<RoomServerEntry?> FindLeastLoadedAsync()
    {
        var database = redis.GetDatabase();
        RoomServerEntry? best = null;

        foreach (var member in await database.SetMembersAsync(NamesKey))
        {
            var name = member.ToString();
            var fields = await database.HashGetAllAsync(EntryKey(name));
            if (fields.Length == 0)
            {
                // The hash expired, so the name is a leftover. Nothing else prunes the set.
                await database.SetRemoveAsync(NamesKey, member);
                continue;
            }

            var entry = ToEntry(name, fields);
            if (entry.RoomCount < entry.Capacity && (best is null || entry.RoomCount < best.RoomCount))
            {
                best = entry;
            }
        }

        return best;
    }

    private static string EntryKey(string name) => $"rs:{name}";

    private static RoomServerEntry ToEntry(string name, HashEntry[] fields)
    {
        var values = fields.AsValueEnumerable().ToDictionary(field => field.Name.ToString(), field => field.Value);

        return new RoomServerEntry(
            name,
            values[ClientEndpointField].ToString(),
            values[ControlEndpointField].ToString(),
            (int)values[RoomCountField],
            (int)values[CapacityField]);
    }
}
