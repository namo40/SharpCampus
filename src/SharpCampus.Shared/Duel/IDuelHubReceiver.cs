using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// What a duel room pushes to the two clients seated in it. Everything here is fire and forget except
/// <see cref="AskRematchAsync"/>, which the room waits on.
/// </summary>
public interface IDuelHubReceiver
{
    /// <summary>Both seats are taken and the countdown has begun.</summary>
    /// <param name="info">Seats, names and how long the countdown runs.</param>
    void OnMatchStarting(MatchStartInfo info);

    /// <summary>One simulated tick has completed.</summary>
    /// <param name="delta">What that tick changed on both boards.</param>
    void OnTickDelta(TickDelta delta);

    /// <summary>The match is over.</summary>
    /// <param name="result">Winner and reason.</param>
    void OnMatchFinished(MatchResult result);

    /// <summary>A seat lost or regained its connection. The match keeps running either way.</summary>
    /// <param name="player">Seat whose connection changed.</param>
    /// <param name="connected">Whether that seat is connected now.</param>
    void OnPresenceChanged(PlayerIndex player, bool connected);

    /// <summary>
    /// Asks whether the client wants to play another game in this room. The room waits for the answer.
    /// </summary>
    /// <param name="timeoutSeconds">How long the offer stays open, so the client can count it down.</param>
    /// <param name="cancellationToken">Cancelled by the room when the offer expires. Always default on the client.</param>
    /// <returns>Whether the client accepts.</returns>
    Task<bool> AskRematchAsync(int timeoutSeconds, CancellationToken cancellationToken = default);

    /// <summary>The rematch offer is over: one side declined, or nobody answered in time.</summary>
    void OnRematchDeclined();
}
