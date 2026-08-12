using SharpCampus.Shared.Internal.Rooms;

namespace SharpCampus.ApiServer.Matchmaking;

public interface IRoomControlClient
{
    Task<CreateRoomResult?> CreateRoomAsync(string controlEndpoint, CreateRoomRequest request);
}
