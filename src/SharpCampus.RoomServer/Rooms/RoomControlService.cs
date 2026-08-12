using MagicOnion;
using MagicOnion.Server;
using SharpCampus.Shared.Internal.Rooms;

namespace SharpCampus.RoomServer.Rooms;

public sealed class RoomControlService(RoomManager rooms) : ServiceBase<IRoomControlService>, IRoomControlService
{
    public UnaryResult<CreateRoomResult> CreateRoomAsync(CreateRoomRequest request)
        => new(new CreateRoomResult(rooms.Create(request.RoomId, request.Players)));
}
