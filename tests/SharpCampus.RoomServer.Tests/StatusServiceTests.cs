using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpCampus.Shared.Services;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public sealed class StatusServiceTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetStatusAsync_ReportsRoomServerIdentity()
    {
        var status = await CreateClient().GetStatusAsync();

        Assert.Equal("SharpCampus.RoomServer", status.ServerName);
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

        return MagicOnionClient.Create<IStatusService>(channel);
    }
}
