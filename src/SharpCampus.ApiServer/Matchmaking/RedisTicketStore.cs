using System.Text.Json;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using StackExchange.Redis;

namespace SharpCampus.ApiServer.Matchmaking;

// Tickets expire together with the entry token they carry, so a player who never turns up leaves
// nothing behind and can queue again.
public sealed class RedisTicketStore(IConnectionMultiplexer redis) : ITicketStore
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromSeconds(60);

    public async Task<MatchTicket?> GetAsync(UserId userId)
    {
        var value = await redis.GetDatabase().StringGetAsync(MatchmakingKeys.Ticket(userId));
        if (value.IsNull)
        {
            return null;
        }

        var stored = JsonSerializer.Deserialize<StoredTicket>(value.ToString(), JsonSerializerOptions.Web)!;
        return new MatchTicket(RoomId.Parse(stored.RoomId), stored.Endpoint, stored.EntryToken);
    }

    public Task StoreAsync(UserId userId, MatchTicket ticket)
    {
        var stored = new StoredTicket(ticket.RoomId.ToString(), ticket.Endpoint, ticket.EntryToken);

        return redis.GetDatabase().StringSetAsync(
            MatchmakingKeys.Ticket(userId),
            JsonSerializer.Serialize(stored, JsonSerializerOptions.Web),
            _lifetime);
    }

    // The wire contract travels as MessagePack; what sits in Redis is plain JSON, so it stays readable
    // from redis-cli while a match is being set up.
    private sealed record StoredTicket(string RoomId, string Endpoint, string EntryToken);
}
