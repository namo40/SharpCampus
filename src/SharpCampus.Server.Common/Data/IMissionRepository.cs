using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

public interface IMissionRepository
{
    // A mission nobody has made progress on has no row, so a day can answer with fewer rows than missions.
    Task<IReadOnlyList<MissionProgress>> GetDailyAsync(UserId userId, DateOnly date);

    // The date is passed in because the day a match counts towards is decided where the match ended.
    Task RecordAsync(UserId userId, DateOnly date, IReadOnlyList<MissionDelta> deltas);

    // The goal and the reward come from master data, not from the row being claimed.
    Task<MissionClaimOutcome> ClaimAsync(UserId userId, DateOnly date, MissionId missionId, int goal, Coins reward);
}
