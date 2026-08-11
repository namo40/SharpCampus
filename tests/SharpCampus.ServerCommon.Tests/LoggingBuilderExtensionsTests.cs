using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpCampus.Server.Common.Logging;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class LoggingBuilderExtensionsTests
{
    [Fact]
    public void AddSharpCampusLogging_ReplacesPreviouslyRegisteredProviders()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddSharpCampusLogging();
        });

        using var provider = services.BuildServiceProvider();
        var loggerProviders = provider.GetServices<ILoggerProvider>().ToArray();

        var loggerProvider = Assert.Single(loggerProviders);
        Assert.StartsWith("ZLogger.", loggerProvider.GetType().FullName);
    }

    [Fact]
    public void AddSharpCampusLogging_ProducesUsableLoggers()
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddSharpCampusLogging());

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<LoggingBuilderExtensionsTests>>();

        logger.ServerStarted("SharpCampus.TestServer", "0.0.0-test");

        Assert.True(logger.IsEnabled(LogLevel.Information));
    }
}
