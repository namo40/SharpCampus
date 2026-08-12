using MessagePack;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// Asks to be seated in a duel room.
/// </summary>
/// <param name="RoomKey">Room to join. The first caller for a key creates the room.</param>
/// <param name="DisplayName">Name shown to the opponent for the length of the match.</param>
[MessagePackObject]
public sealed record JoinRoomRequest(
    [property: Key(0)] string RoomKey,
    [property: Key(1)] string DisplayName);
