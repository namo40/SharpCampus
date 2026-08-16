using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SharpCampus.Server.Common.Observability;
using StackExchange.Redis;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class RedisHealthCheckTests
{
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly IConnectionMultiplexer _redis = Substitute.For<IConnectionMultiplexer>();

    public RedisHealthCheckTests() => _redis.GetDatabase().Returns(_database);

    [Fact]
    public async Task RedisThatAnswersAPing_IsHealthy()
    {
        _database.PingAsync().Returns(TimeSpan.FromMilliseconds(1));

        Assert.Equal(HealthStatus.Healthy, await CheckAsync());
    }

    [Fact]
    public async Task RedisThatCannotBeReached_IsUnhealthy()
    {
        _database.PingAsync().ThrowsAsync(
            new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "down"));

        Assert.Equal(HealthStatus.Unhealthy, await CheckAsync());
    }

    private async Task<HealthStatus> CheckAsync() =>
        (await new RedisHealthCheck(_redis).CheckHealthAsync(new HealthCheckContext())).Status;
}
