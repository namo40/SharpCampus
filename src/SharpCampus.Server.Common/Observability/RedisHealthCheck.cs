using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace SharpCampus.Server.Common.Observability;

// Matchmaking, the room registry and the leaderboards all live in Redis, so an instance that cannot reach
// it has nothing to serve even though its process is fine.
public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            // A probe answers rather than throws: whatever went wrong on the way to Redis is the answer.
            return HealthCheckResult.Unhealthy("Redis did not answer a ping.", exception);
        }
    }
}
