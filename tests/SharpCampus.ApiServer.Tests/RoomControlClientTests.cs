using SharpCampus.ApiServer.Matchmaking;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public class RoomControlClientTests
{
    // Nothing listens on port 1, which is what a room server that is not running looks like from here.
    // The address is literal so the attempt fails on the refused connection rather than on name resolution.
    private const string DeadEndpoint = "http://127.0.0.1:1";

    [Fact]
    public async Task UnreachableRoomServer_AnswersNullRatherThanThrowing()
    {
        using var client = new RoomControlClient();

        // The pairing worker reads this null as "leave the pair queued", so it must not surface as an
        // exception that would take the worker's whole pass down with it.
        Assert.Null(await client.CreateRoomAsync(DeadEndpoint, Request()));
    }

    [Fact]
    public async Task Channel_IsReusedForTheSameEndpoint()
    {
        var client = new RoomControlClient();

        Assert.Null(await client.CreateRoomAsync(DeadEndpoint, Request()));

        client.Dispose();

        // Disposal closes the channels the client cached. A call that opened a fresh channel every time
        // would answer null again here, so the disposed channel showing through is what pins the reuse.
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.CreateRoomAsync(DeadEndpoint, Request()));
    }

    private static CreateRoomRequest Request() => new(
        new RoomId(Ulid.NewUlid()),
        [
            new RoomPlayer(new UserId(Guid.NewGuid()), "alpha"),
            new RoomPlayer(new UserId(Guid.NewGuid()), "beta"),
        ]);
}
