using System.Runtime.CompilerServices;
using StackExchange.Redis;

namespace SharpCampus.ServerCommon.Tests;

// The development Redis container, or nothing when it is not running: a fresh clone runs the suite
// without any infrastructure, and a machine with the container gets the coverage.
// Tests that need it declare <c>[Fact(Skip = LocalRedis.SkipMessage, SkipUnless = nameof(LocalRedis.IsRunning), SkipType = typeof(LocalRedis))]</c>.
internal static class LocalRedis
{
    // Reported for every test that needs a container this machine is not running.
    public const string SkipMessage = "Redis is not listening on localhost:6379.";

    private static IConnectionMultiplexer? _connection;

    public static bool IsRunning => _connection is not null;

    public static IConnectionMultiplexer Connection =>
        _connection ?? throw new InvalidOperationException("Redis is not running; the test should have been skipped.");

    // Probed once, before the runner starts anything. Leaving it to the first test that asks would put
    // a blocking connect attempt in the middle of a parallel run, and the thread it holds for the
    // timeout is one the other tests' hosts need.
    [ModuleInitializer]
    internal static void Probe()
    {
        try
        {
            _connection = ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                EndPoints = { "localhost:6379" },
                ConnectTimeout = 500,
                ConnectRetry = 1,
            });
        }
        catch (RedisConnectionException)
        {
            _connection = null;
        }
    }
}
