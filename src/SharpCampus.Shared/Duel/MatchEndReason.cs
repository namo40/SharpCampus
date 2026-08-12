namespace SharpCampus.Shared.Duel;

/// <summary>
/// Why a match stopped.
/// </summary>
public enum MatchEndReason : byte
{
    /// <summary>A board could no longer fit a piece.</summary>
    TopOut = 0,

    /// <summary>A player gave up.</summary>
    Forfeit = 1,

    /// <summary>A player's connection dropped.</summary>
    Disconnect = 2,
}
