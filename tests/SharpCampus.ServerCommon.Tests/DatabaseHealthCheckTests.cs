using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.Observability;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class DatabaseHealthCheckTests
{
    // Nothing listens on port 1, and the one second connect timeout keeps the failure from taking the
    // Npgsql default of fifteen. The healthy answer is what a running stack shows on /readyz.
    private const string Unreachable = "Host=127.0.0.1;Port=1;Username=postgres;Password=postgres;Timeout=1";

    [Fact]
    public async Task DatabaseThatCannotBeReached_IsUnhealthy()
    {
        await using var database = new SharpCampusDbContext(new DbContextOptionsBuilder<SharpCampusDbContext>()
            .UseNpgsql(Unreachable)
            .Options);

        var result = await new DatabaseHealthCheck(database)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
