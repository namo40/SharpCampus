using System.Net;
using NSubstitute;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

// The route the entry point resolves a room with. It is plain HTTP/1 rather than a MagicOnion service
// because the caller is a proxy filter, which speaks nothing else.
public sealed class RoomLocationEndpointTests(SupabaseTestFactory factory) : IClassFixture<SupabaseTestFactory>
{
    private readonly IRoomLocationStore _locations = Substitute.For<IRoomLocationStore>();

    [Fact]
    public async Task RoomThatIsHeld_AnswersWithTheServerNameAlone()
    {
        var roomId = new RoomId(Ulid.NewUlid());
        _locations.FindAsync(roomId).Returns("SharpCampus.RoomServer.1");

        var response = await GetAsync(roomId.ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "SharpCampus.RoomServer.1",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RoomNobodyHolds_IsNotFound()
    {
        // Spelled out because a substitute answers a string with an empty one rather than with nothing.
        _locations.FindAsync(Arg.Any<RoomId>()).Returns((string?)null);

        var response = await GetAsync(new RoomId(Ulid.NewUlid()).ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IdThatIsNotARoomId_IsNotFoundRatherThanAFailure()
    {
        var response = await GetAsync("not-a-room-id");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await _locations.DidNotReceive().FindAsync(Arg.Any<RoomId>());
    }

    private Task<HttpResponseMessage> GetAsync(string roomId) =>
        factory.WithRoomLocations(_locations)
            .CreateClient()
            .GetAsync($"/internal/rooms/{roomId}/location", TestContext.Current.CancellationToken);
}
