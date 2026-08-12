using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// Everything a paired client needs to enter its room. Expires a minute after it is issued.
/// </summary>
/// <param name="RoomId">Room the caller was assigned to.</param>
/// <param name="Endpoint">Address of the room server hosting it.</param>
/// <param name="EntryToken">Signed proof that this account belongs in that room.</param>
[MessagePackObject]
public sealed record MatchTicket(
    [property: Key(0)] RoomId RoomId,
    [property: Key(1)] string Endpoint,
    [property: Key(2)] string EntryToken);
