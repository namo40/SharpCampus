using MessagePack;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// Who occupies one seat of a match.
/// </summary>
/// <param name="PlayerIndex">Seat being described.</param>
/// <param name="DisplayName">Name to show for this seat.</param>
/// <param name="Skin">
/// Resolved skin this seat's board is drawn with. The record travels whole because the client holds no
/// master data to look an identifier up in.
/// </param>
[MessagePackObject]
public sealed record MatchPlayerInfo(
    [property: Key(0)] PlayerIndex PlayerIndex,
    [property: Key(1)] string DisplayName,
    [property: Key(2)] Skin Skin);
