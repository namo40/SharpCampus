using MagicOnion;
using MagicOnion.Server;
using SharpCampus.Shared.Internal.Rooms;

namespace SharpCampus.RoomServer.Rooms;

public sealed class RoomControlService(RoomManager rooms) : ServiceBase<IRoomControlService>, IRoomControlService
{
    public async UnaryResult<CreateRoomResult> CreateRoomAsync(CreateRoomRequest request)
        => new(await rooms.CreateAsync(request.RoomId, request.Players));
}
