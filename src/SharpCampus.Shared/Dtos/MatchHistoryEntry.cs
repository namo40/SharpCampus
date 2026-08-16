using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// One settled game, told from the seat of the player asking for it. Carries no match identifier:
/// a history says what happened, not which room it happened in.
/// </summary>
/// <param name="PlayedAt">When the settlement was written, stamped by the database in UTC.</param>
/// <param name="OpponentNickname">
/// Nickname of the player on the other side. Empty when the game's other row is missing.
/// </param>
/// <param name="Outcome">
/// How the game ended for the caller, exactly as settlement stored it: <c>win</c>, <c>loss</c> or
/// <c>draw</c>. Deliberately a self-describing string rather than an enum, so a stored value older or
/// newer than the client reading it still arrives intact instead of failing to deserialize.
/// </param>
/// <param name="EndReason">
/// What ended the game, exactly as settlement stored it: <c>top_out</c>, <c>forfeit</c> or
/// <c>disconnect</c>. A string for the same reason <paramref name="Outcome"/> is one.
/// </param>
/// <param name="RatingBefore">Rating the caller carried into the game.</param>
/// <param name="RatingAfter">Rating the game left the caller with. A draw leaves it unchanged.</param>
/// <param name="CoinsAwarded">Coins the game paid the caller.</param>
[MessagePackObject]
public sealed record MatchHistoryEntry(
    [property: Key(0)] DateTimeOffset PlayedAt,
    [property: Key(1)] string OpponentNickname,
    [property: Key(2)] string Outcome,
    [property: Key(3)] string EndReason,
    [property: Key(4)] Rating RatingBefore,
    [property: Key(5)] Rating RatingAfter,
    [property: Key(6)] Coins CoinsAwarded);
