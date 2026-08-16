using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using SharpCampus.Server.Common.Configuration;

namespace SharpCampus.Server.Common.Observability;

public static class ObservabilityExtensions
{
    // Npgsql waits 15 seconds before giving up on a connection, which outlasts any probe period a
    // scheduler would use. The timeout is what turns a hang into an answer.
    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(3);

    // Everything the hosts have in common: MagicOnion counts streaming hub connections and call
    // durations, and the runtime and Kestrel meters are built into .NET rather than instrumented here.
    private static readonly string[] _sharedMeters =
    [
        "MagicOnion.Server",
        "System.Runtime",
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
    ];

    public static IServiceCollection AddSharpCampusObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string meterName,
        Action<MeterProviderBuilder>? configureMetrics = null,
        Action<IHealthChecksBuilder>? configureChecks = null)
    {
        var serverName = (configuration.GetSection(ServerOptions.SectionName).Get<ServerOptions>()
            ?? new ServerOptions()).Name;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serverName))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(_sharedMeters);
                metrics.AddMeter(meterName);
                configureMetrics?.Invoke(metrics);

                // Nothing is pushed anywhere: the endpoint below is read by whoever scrapes it.
                metrics.AddPrometheusExporter();
            });

        // Registered whether or not anything is added to it: a server with no dependency to check still
        // answers both probes, and readiness with nothing behind it is readiness met.
        var checks = services.AddHealthChecks();
        configureChecks?.Invoke(checks);
        return services;
    }

    public static IHealthChecksBuilder AddRedisCheck(this IHealthChecksBuilder checks)
        => checks.AddCheck<RedisHealthCheck>("redis", timeout: _probeTimeout);

    public static IHealthChecksBuilder AddDatabaseCheck(this IHealthChecksBuilder checks)
        => checks.AddCheck<DatabaseHealthCheck>("database", timeout: _probeTimeout);

    public static IEndpointRouteBuilder MapSharpCampusObservability(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPrometheusScrapingEndpoint();

        // Liveness is about this process and nothing behind it: a restart cannot fix a Redis that is
        // down, so no dependency may answer for it.
        endpoints.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });

        // Readiness is the opposite: an instance whose dependencies are unreachable is one no traffic
        // should be sent to yet.
        endpoints.MapHealthChecks("/readyz");
        return endpoints;
    }
}
