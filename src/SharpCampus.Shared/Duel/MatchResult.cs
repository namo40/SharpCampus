using MessagePack;
using SharpCampus.GameCore;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// How a match ended. The last message a room sends.
/// </summary>
/// <param name="Outcome">Which side won, or that it was a draw.</param>
/// <param name="WinnerPlayerIndex">Seat of the winner, or null for a draw.</param>
/// <param name="Reason">What ended the match.</param>
[MessagePackObject]
public sealed record MatchResult(
    [property: Key(0)] DuelOutcome Outcome,
    [property: Key(1)] PlayerIndex? WinnerPlayerIndex,
    [property: Key(2)] MatchEndReason Reason)
{
    /// <summary>Maps an outcome to the seat that won it.</summary>
    /// <param name="outcome">Outcome the simulation reported.</param>
    /// <returns>Seat of the winner, or null when there is none.</returns>
    public static PlayerIndex? WinnerOf(DuelOutcome outcome) => outcome switch
    {
        DuelOutcome.Player1Wins => new PlayerIndex(0),
        DuelOutcome.Player2Wins => new PlayerIndex(1),
        _ => null,
    };
}
