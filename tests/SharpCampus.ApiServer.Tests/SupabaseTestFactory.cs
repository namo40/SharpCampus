using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

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

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                    new OpenIdConnectConfiguration
                    {
                        Issuer = Issuer,
                        SigningKeys = { new ECDsaSecurityKey(_signingAlgorithm) },
                    });

                options.TokenValidationParameters.ValidIssuer = Issuer;
            }));

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
