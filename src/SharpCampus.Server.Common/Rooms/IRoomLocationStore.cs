using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Rooms;

public interface IRoomLocationStore
{
    // Written by the server that owns the room, read by whatever routes a client to it.
    Task StoreAsync(RoomId roomId, string serverName);

    Task RemoveAsync(RoomId roomId);

    // Returns null when no server holds the room.
    Task<string?> FindAsync(RoomId roomId);
}
