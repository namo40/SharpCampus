using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// Who occupies one seat of a match.
/// </summary>
/// <param name="PlayerIndex">Seat being described.</param>
/// <param name="DisplayName">Name to show for this seat.</param>
[MessagePackObject]
public sealed record MatchPlayerInfo(
    [property: Key(0)] PlayerIndex PlayerIndex,
    [property: Key(1)] string DisplayName);
