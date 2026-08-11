using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Configuration;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class ServerOptionsTests
{
    [Fact]
    public void AddServerOptions_BindsConfiguredSection()
    {
        var options = Resolve(new Dictionary<string, string?> { ["Server:Name"] = "SharpCampus.TestServer" });

        Assert.Equal("SharpCampus.TestServer", options.Name);
    }

    [Fact]
    public void AddServerOptions_KeepsDefaultWhenSectionMissing()
    {
        var options = Resolve(new Dictionary<string, string?> { ["Unrelated:Key"] = "value" });

        Assert.Equal("SharpCampus", options.Name);
    }

    private static ServerOptions Resolve(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddServerOptions(configuration);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ServerOptions>>().Value;
    }
}
