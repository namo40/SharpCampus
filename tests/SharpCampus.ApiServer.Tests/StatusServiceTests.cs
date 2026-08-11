using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpCampus.Shared.Services;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public sealed class StatusServiceTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly List<GrpcChannel> _channels = [];

    public void Dispose()
    {
        foreach (var channel in _channels)
        {
            channel.Dispose();
        }
    }

    [Fact]
    public async Task GetStatusAsync_ReportsApiServerIdentity()
    {
        var status = await CreateClient().GetStatusAsync();

        Assert.Equal("SharpCampus.ApiServer", status.ServerName);
        Assert.NotEmpty(status.Version);
    }

    [Fact]
    public async Task GetStatusAsync_ReportsCurrentUtcTime()
    {
        var before = DateTime.UtcNow;
        var status = await CreateClient().GetStatusAsync();

        Assert.InRange(status.TimestampUtc, before, DateTime.UtcNow);
    }

    private IStatusService CreateClient()
    {
        var channel = GrpcChannel.ForAddress(
            factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() });
        _channels.Add(channel);

        return MagicOnionClient.Create<IStatusService>(channel);
    }
}
