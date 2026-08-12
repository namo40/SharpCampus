namespace SharpCampus.Server.Common.Rooms;

public interface IRoomRegistry
{
    // Also the heartbeat: an entry that stops being written expires and stops being handed out.
    Task RegisterAsync(RoomServerEntry entry);

    Task RemoveAsync(string name);

    // Returns null when no registered server is alive and below capacity.
    Task<RoomServerEntry?> FindLeastLoadedAsync();
}
