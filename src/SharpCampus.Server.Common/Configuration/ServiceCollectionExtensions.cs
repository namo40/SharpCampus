using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Server.Common.Data;

namespace SharpCampus.Server.Common.Configuration;

public static class ServiceCollectionExtensions
{
    // Supabase issues access tokens to the "authenticated" audience for every signed-in user.
    private const string SupabaseAudience = "authenticated";

    public static IServiceCollection AddServerOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ServerOptions>(configuration.GetSection(ServerOptions.SectionName));
        return services;
    }

    public static IServiceCollection AddSupabaseJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(SupabaseOptions.SectionName);
        services.Configure<SupabaseOptions>(section);

        var authority = (section.Get<SupabaseOptions>() ?? new SupabaseOptions()).Authority;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Supabase signs access tokens with a rotating asymmetric key, so the signing keys are
                // discovered from the project's JWKS endpoint rather than configured as a shared secret.
                options.Authority = authority;
                options.RequireHttpsMetadata = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

                // Without this, `sub` and `email` arrive renamed to their WS-Federation URIs.
                options.MapInboundClaims = false;

                options.TokenValidationParameters.ValidIssuer = authority;
                options.TokenValidationParameters.ValidAudience = SupabaseAudience;
            });

        services.AddAuthorization();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        // Pooling hands each request a reset context instead of a new one, which matters because building
        // the model is the expensive part and a game server opens one of these per call.
        services.AddDbContextPool<SharpCampusDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));

        services.AddScoped<IProfileRepository, ProfileRepository>();
        return services;
    }
}
