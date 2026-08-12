using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// Asks to be seated in a duel room the caller was matched into.
/// </summary>
/// <param name="RoomId">Room to enter, as issued in the match ticket.</param>
/// <param name="EntryToken">Signed proof from the match ticket that this account belongs in that room.</param>
[MessagePackObject]
public sealed record JoinRoomRequest(
    [property: Key(0)] RoomId RoomId,
    [property: Key(1)] string EntryToken);
