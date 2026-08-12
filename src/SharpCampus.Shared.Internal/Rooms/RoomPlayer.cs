using MessagePack;
using SharpCampus.Shared.Identity;

namespace SharpCampus.Shared.Internal.Rooms;

/// <summary>
/// One of the two accounts a room is reserved for.
/// </summary>
/// <param name="UserId">Account allowed to take the seat.</param>
/// <param name="DisplayName">Name the opponent sees. Comes from the profile, never from the client.</param>
[MessagePackObject]
public sealed record RoomPlayer(
    [property: Key(0)] UserId UserId,
    [property: Key(1)] string DisplayName);
