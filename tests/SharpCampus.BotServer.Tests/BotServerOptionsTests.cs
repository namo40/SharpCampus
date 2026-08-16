using SharpCampus.BotServer.Configuration;
using Xunit;

namespace SharpCampus.BotServer.Tests;

public sealed class BotServerOptionsTests
{
    [Fact]
    public void TicketEndpoint_IsFollowedAsWritten()
    {
        var options = new BotServerOptions();

        Assert.Equal("http://localhost:5000", options.RoomEndpoint("http://localhost:5000"));
    }

    [Fact]
    public void ConfiguredOverride_ReplacesWhatTheTicketSays()
    {
        var options = new BotServerOptions { RoomEndpointOverride = "http://envoy:5000" };

        Assert.Equal("http://envoy:5000", options.RoomEndpoint("http://localhost:5000"));
    }
}
