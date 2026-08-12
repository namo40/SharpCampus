using SharpCampus.Shared.Services;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public sealed class StatusServiceTests(SupabaseTestFactory factory) : IClassFixture<SupabaseTestFactory>, IDisposable
{
    private readonly MagicOnionTestClient _client = new();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task GetStatusAsync_ReportsApiServerIdentity()
    {
        var status = await _client.Create<IStatusService>(factory).GetStatusAsync();

        Assert.Equal("SharpCampus.ApiServer", status.ServerName);
        Assert.NotEmpty(status.Version);
    }

    [Fact]
    public async Task GetStatusAsync_ReportsCurrentUtcTime()
    {
        var before = DateTime.UtcNow;
        var status = await _client.Create<IStatusService>(factory).GetStatusAsync();

        Assert.InRange(status.TimestampUtc, before, DateTime.UtcNow);
    }
}
