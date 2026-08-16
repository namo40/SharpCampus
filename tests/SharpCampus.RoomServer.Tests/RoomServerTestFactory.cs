using System.Security.Cryptography;
using Grpc.Net.Client;
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
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Server.Common.Security;
using StackExchange.Redis;

namespace SharpCampus.RoomServer.Tests;

// Replaces the Supabase JWKS lookup with a locally generated key and the room registry's Redis with a
// double, so the suite needs neither a Supabase stack nor a Redis container.
public sealed class RoomServerTestFactory : WebApplicationFactory<Program>
{
    private const string Issuer = "http://supabase.test/auth/v1";
    private const string Audience = "authenticated";

    private readonly ECDsa _signingAlgorithm = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public EntryTokenService EntryTokens => Services.GetRequiredService<EntryTokenService>();

    internal RoomManager Rooms => Services.GetRequiredService<RoomManager>();

    public string CreateToken(Guid userId) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = Issuer,
        Audience = Audience,
        Expires = DateTime.UtcNow.AddMinutes(5),
        Claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = userId.ToString(),
            [JwtRegisteredClaimNames.Email] = $"{userId:N}@sharpcampus.test",
        },
        SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(_signingAlgorithm), SecurityAlgorithms.EcdsaSha256),
    });

    public GrpcChannel CreateChannel() => GrpcChannel.ForAddress(
        Server.BaseAddress,
        new GrpcChannelOptions { HttpHandler = Server.CreateHandler() });

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

            // The registry heartbeat is the only hosted service here that talks to Redis, and what a room
            // itself writes there is doubled below.
            services.RemoveAll<IHostedService>();
            services.AddSingleton(Substitute.For<IConnectionMultiplexer>());
            services.AddSingleton(Substitute.For<IActiveRoomStore>());
            services.AddSingleton(Substitute.For<IRoomLocationStore>());
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _signingAlgorithm.Dispose();
        }
    }
}
