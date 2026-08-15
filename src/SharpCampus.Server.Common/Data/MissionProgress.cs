using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

// One row per account, day and mission. The day is part of the key rather than a column that gets
// reset, so a mission starts over simply by being asked about under tomorrow's date.
public sealed record MissionProgress(UserId UserId, DateOnly MissionDate, MissionId MissionId)
{
    public int Progress { get; init; }

    public bool Claimed { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
