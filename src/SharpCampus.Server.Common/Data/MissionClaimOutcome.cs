namespace SharpCampus.Server.Common.Data;

// What the claim transaction settled on. Server side only: the service maps it onto the contract's result.
public enum MissionClaimOutcome
{
    Claimed,
    NotCompleted,
    AlreadyClaimed,
}
