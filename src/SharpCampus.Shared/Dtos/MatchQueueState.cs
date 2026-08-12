namespace SharpCampus.Shared.Dtos;

/// <summary>
/// Where the caller stands in matchmaking.
/// </summary>
public enum MatchQueueState : byte
{
    /// <summary>Not in the queue and holding no ticket.</summary>
    None = 0,

    /// <summary>Waiting for an opponent.</summary>
    Queued = 1,

    /// <summary>Paired with an opponent: the ticket says which room to enter.</summary>
    Matched = 2,
}
