using SharpCampus.Server.Common.Rooms;
using SharpCampus.Shared.Values;

namespace SharpCampus.ApiServer.Rooms;

public static class RoomLocationEndpoint
{
    // Answers the entry point's routing filter from inside the network, on the same HTTP/1 surface the
    // probes and the scrape sit on. No route of the entry point's own exposes it to clients.
    public static IEndpointRouteBuilder MapRoomLocation(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/internal/rooms/{roomId}/location", async (string roomId, IRoomLocationStore locations) =>
        {
            if (!RoomId.TryParse(roomId, out var parsed))
            {
                return Results.NotFound();
            }

            return await locations.FindAsync(parsed) is { } serverName
                ? Results.Text(serverName)
                : Results.NotFound();
        });

        return endpoints;
    }
}
