using StackExchange.Redis;

namespace SharpCampus.ApiServer.Matchmaking;

// A key that expires on its own: the cooldown ends whether or not the instance that started it is
// still running, and it is shared so two instances cannot each send for a bot.
public sealed class RedisBotSummonCooldown(IConnectionMultiplexer redis) : IBotSummonCooldown
{
    private const string CooldownKey = "mm:bot-summoned";
    private static readonly TimeSpan _lifetime = TimeSpan.FromSeconds(30);

    public Task<bool> IsActiveAsync() => redis.GetDatabase().KeyExistsAsync(CooldownKey);

    public Task StartAsync() => redis.GetDatabase().StringSetAsync(CooldownKey, Environment.MachineName, _lifetime);
}
