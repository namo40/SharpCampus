using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.Leaderboards;
using SharpCampus.Server.Common.MasterData;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Server.Common.Security;
using StackExchange.Redis;

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
        services.AddScoped<IShopRepository, ShopRepository>();
        services.AddScoped<IMissionRepository, MissionRepository>();
        return services;
    }

    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(RedisOptions.SectionName);
        services.Configure<RedisOptions>(section);

        var connectionString = (section.Get<RedisOptions>() ?? new RedisOptions()).ConnectionString;

        // Connect throws when Redis is unreachable, and the background services that pair players and
        // publish the registry take this on construction, so an unreachable Redis stops startup.
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<IRoomRegistry, RedisRoomRegistry>();
        services.AddSingleton<IActiveRoomStore, RedisActiveRoomStore>();
        services.AddSingleton<ILeaderboardStore, RedisLeaderboardStore>();
        return services;
    }

    public static IServiceCollection AddEntryTokens(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EntryTokenOptions>(configuration.GetSection(EntryTokenOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<EntryTokenService>();
        return services;
    }

    public static IServiceCollection AddMasterData(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(MasterDataOptions.SectionName);
        services.Configure<MasterDataOptions>(section);

        var path = (section.Get<MasterDataOptions>() ?? new MasterDataOptions()).Path;

        // Loaded here rather than from a factory so missing or invalid data stops startup instead of
        // the first request that needs it.
        services.AddSingleton(MasterDataLoader.Load(path));
        return services;
    }
}
