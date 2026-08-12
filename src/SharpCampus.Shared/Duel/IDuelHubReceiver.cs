namespace SharpCampus.Shared.Duel;

/// <summary>
/// What a duel room pushes to the two clients seated in it. Every method is fire and forget.
/// </summary>
public interface IDuelHubReceiver
{
    /// <summary>Both seats are taken and the countdown has begun.</summary>
    /// <param name="info">Seats, names and how long the countdown runs.</param>
    void OnMatchStarting(MatchStartInfo info);

    /// <summary>One simulated tick has completed.</summary>
    /// <param name="delta">What that tick changed on both boards.</param>
    void OnTickDelta(TickDelta delta);

    /// <summary>The match is over and the room is closing.</summary>
    /// <param name="result">Winner and reason.</param>
    void OnMatchFinished(MatchResult result);
}
