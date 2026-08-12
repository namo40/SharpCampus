using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// Outcome of a seating request.
/// </summary>
/// <param name="Accepted">Whether the caller holds a seat in the room.</param>
/// <param name="PlayerIndex">Seat the caller was given, or null when it was not seated.</param>
/// <param name="WaitingForOpponent">Whether the other seat is still empty.</param>
[MessagePackObject]
public sealed record JoinRoomResult(
    [property: Key(0)] bool Accepted,
    [property: Key(1)] PlayerIndex? PlayerIndex,
    [property: Key(2)] bool WaitingForOpponent)
{
    /// <summary>Seat that is not free: the room is full, already playing, or gone.</summary>
    public static JoinRoomResult Rejected { get; } = new(false, null, false);
}
