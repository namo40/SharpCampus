using MagicOnion;

namespace SharpCampus.Shared.Internal.Rooms;

/// <summary>
/// What the ApiServer asks of a room server. Server to server only: never reachable from a game client.
/// </summary>
// Production note: this contract carries no caller authentication because it is only routed inside the
// cluster. A real deployment would pin it behind mTLS and a NetworkPolicy rather than trust the network.
public interface IRoomControlService : IService<IRoomControlService>
{
    /// <summary>
    /// Stands up a room for a matched pair. The room seats only those two accounts.
    /// </summary>
    /// <param name="request">Room identifier and the pair it is reserved for.</param>
    /// <returns>Whether the room was created.</returns>
    UnaryResult<CreateRoomResult> CreateRoomAsync(CreateRoomRequest request);
}
