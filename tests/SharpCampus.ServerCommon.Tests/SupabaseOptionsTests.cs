using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Configuration;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class SupabaseOptionsTests
{
    [Fact]
    public void AddSupabaseJwtAuthentication_BindsConfiguredSection()
    {
        var options = Resolve("http://127.0.0.1:54321");

        Assert.Equal("http://127.0.0.1:54321", options.Url);
    }

    [Theory]
    [InlineData("http://127.0.0.1:54321")]
    [InlineData("http://127.0.0.1:54321/")]
    public void Authority_IsTheGoTrueBasePathOfTheProject(string url)
    {
        Assert.Equal("http://127.0.0.1:54321/auth/v1", Resolve(url).Authority);
    }

    [Fact]
    public void IssuerOfItsOwn_IsWhereTheAuthorityIs()
    {
        var options = Resolve("http://127.0.0.1:54321");

        Assert.Equal(options.Authority, options.Issuer);
    }

    [Theory]
    [InlineData("http://127.0.0.1:54321")]
    [InlineData("http://127.0.0.1:54321/")]
    public void ConfiguredIssuerUrl_MovesTheIssuerAndLeavesTheAuthorityWhereItWas(string issuerUrl)
    {
        // What a container reaches GoTrue at and what GoTrue writes into `iss` are two different addresses.
        var options = Resolve("http://host.docker.internal:54321", issuerUrl);

        Assert.Equal("http://127.0.0.1:54321/auth/v1", options.Issuer);
        Assert.Equal("http://host.docker.internal:54321/auth/v1", options.Authority);
    }

    private static SupabaseOptions Resolve(string url, string? issuerUrl = null)
    {
        var settings = new Dictionary<string, string?> { ["Supabase:Url"] = url };
        if (issuerUrl is not null)
        {
            settings["Supabase:IssuerUrl"] = issuerUrl;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSupabaseJwtAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<SupabaseOptions>>().Value;
    }
}
