using MessagePack;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Internal.Rooms;

/// <summary>
/// One of the two accounts a room is reserved for.
/// </summary>
/// <param name="UserId">Account allowed to take the seat.</param>
/// <param name="DisplayName">Name the opponent sees. Comes from the profile, never from the client.</param>
/// <param name="EquippedSkinId">
/// Skin the profile had equipped when the pair was made. Only the identifier travels: the room server
/// resolves it against its own master data.
/// </param>
[MessagePackObject]
public sealed record RoomPlayer(
    [property: Key(0)] UserId UserId,
    [property: Key(1)] string DisplayName,
    [property: Key(2)] SkinId EquippedSkinId);
