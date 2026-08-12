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

    private static SupabaseOptions Resolve(string url)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Supabase:Url"] = url })
            .Build();

        var services = new ServiceCollection();
        services.AddSupabaseJwtAuthentication(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<SupabaseOptions>>().Value;
    }
}
