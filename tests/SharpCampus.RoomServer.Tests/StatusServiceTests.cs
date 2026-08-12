using MagicOnion.Client;
using SharpCampus.Shared.Serialization;
using SharpCampus.Shared.Services;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public sealed class StatusServiceTests(RoomServerTestFactory factory) : IClassFixture<RoomServerTestFactory>
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
        => MagicOnionClient.Create<IStatusService>(factory.CreateChannel(), ContractSerialization.Provider);
}
