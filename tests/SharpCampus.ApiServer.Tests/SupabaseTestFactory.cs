using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.Leaderboards;
using StackExchange.Redis;

namespace SharpCampus.ApiServer.Tests;

/// Replaces the Supabase JWKS lookup with a locally generated key, so the suite needs no running Supabase stack.
public sealed class SupabaseTestFactory : WebApplicationFactory<Program>
{
    private const string Issuer = "http://supabase.test/auth/v1";
    private const string Audience = "authenticated";

    private readonly ECDsa _signingAlgorithm = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _foreignAlgorithm = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string CreateToken(Guid userId, string email) =>
        CreateToken(userId, email, DateTime.UtcNow.AddMinutes(5), _signingAlgorithm);

    public string CreateExpiredToken(Guid userId, string email) =>
        // Well past the 5 minute clock skew that token validation allows by default.
        CreateToken(userId, email, DateTime.UtcNow.AddMinutes(-30), _signingAlgorithm);

    public string CreateForeignlySignedToken(Guid userId, string email) =>
        CreateToken(userId, email, DateTime.UtcNow.AddMinutes(5), _foreignAlgorithm);

    // Swaps the profile store for a test double, so the suite needs no Postgres either.
    public WebApplicationFactory<Program> WithProfiles(IProfileRepository profiles) =>
        WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped(_ => profiles)));

    // The shop reads the profile and writes the purchase through two stores, so both are doubled together.
    public WebApplicationFactory<Program> WithShop(IProfileRepository profiles, IShopRepository shop) =>
        WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => profiles);
                services.AddScoped(_ => shop);
            }));

    // Missions materialize the profile the shop does and keep progress in a store of their own.
    public WebApplicationFactory<Program> WithMissions(IProfileRepository profiles, IMissionRepository missions) =>
        WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => profiles);
                services.AddScoped(_ => missions);
            }));

    // The places come from Redis and the names beside them from the profile store, the cache decides
    // whether either is read at all, and the clock decides which day's board a call even asks for.
    public WebApplicationFactory<Program> WithLeaderboards(
        IProfileRepository profiles,
        ILeaderboardStore leaderboard,
        ILeaderboardCache cache,
        TimeProvider time) =>
        WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => profiles);
                services.AddSingleton(leaderboard);
                services.AddSingleton(cache);
                services.AddSingleton(time);
            }));

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                    new OpenIdConnectConfiguration
                    {
                        Issuer = Issuer,
                        SigningKeys = { new ECDsaSecurityKey(_signingAlgorithm) },
                    });

                options.TokenValidationParameters.ValidIssuer = Issuer;
            });

            // Nothing under test here needs Redis, and the pairing worker is covered on its own, so the
            // suite runs without the container the real server insists on.
            services.RemoveAll<IHostedService>();
            services.AddSingleton(Substitute.For<IConnectionMultiplexer>());
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _signingAlgorithm.Dispose();
            _foreignAlgorithm.Dispose();
        }
    }

    private static string CreateToken(Guid userId, string email, DateTime expires, ECDsa algorithm) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = expires,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Email] = email,
            },
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(algorithm), SecurityAlgorithms.EcdsaSha256),
        });
}
