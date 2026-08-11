using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharpCampus.Server.Common.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServerOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServerOptions>(configuration.GetSection(ServerOptions.SectionName));
        return services;
    }
}
