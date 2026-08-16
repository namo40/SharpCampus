using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharpCampus.Server.Common.Data;

namespace SharpCampus.Server.Common.Observability;

// Health checks are resolved in a scope of their own, which is what lets this take the scoped context the
// rest of the server reads profiles through.
public sealed class DatabaseHealthCheck(SharpCampusDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("The database refused a connection.");
        }
        catch (Exception exception)
        {
            // A probe answers rather than throws: whatever went wrong on the way to Postgres is the answer.
            return HealthCheckResult.Unhealthy("The database refused a connection.", exception);
        }
    }
}
