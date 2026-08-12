using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Configuration;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class DatabaseOptionsTests
{
    [Fact]
    public void AddDatabase_BindsConfiguredSection()
    {
        const string connectionString = "Host=127.0.0.1;Port=54322;Username=postgres;Database=postgres";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = connectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddDatabase(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Equal(connectionString, provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString);
    }
}
