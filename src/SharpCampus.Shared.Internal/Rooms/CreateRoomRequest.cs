using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Internal.Rooms;

/// <summary>
/// Asks a room server to stand up a room for a pair the queue just matched.
/// </summary>
/// <param name="RoomId">Identifier the room will answer to.</param>
/// <param name="Players">The two accounts the room is reserved for, in seat order.</param>
[MessagePackObject]
public sealed record CreateRoomRequest(
    [property: Key(0)] RoomId RoomId,
    [property: Key(1)] RoomPlayer[] Players);
