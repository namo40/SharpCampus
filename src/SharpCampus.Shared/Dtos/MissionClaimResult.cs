namespace SharpCampus.Shared.Dtos;

/// <summary>
/// Outcome of a daily mission claim.
/// </summary>
public enum MissionClaimResult
{
    /// <summary>The reward has been credited to the account.</summary>
    Claimed,

    /// <summary>Today's progress has not reached the goal yet, so nothing was paid.</summary>
    NotCompleted,

    /// <summary>The reward was already taken today, so nothing was paid again.</summary>
    AlreadyClaimed,

    /// <summary>Master data holds no mission under that identifier.</summary>
    UnknownMission,
}
