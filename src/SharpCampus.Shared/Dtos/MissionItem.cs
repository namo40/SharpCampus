using MessagePack;
using SharpCampus.Shared.MasterData;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// One daily mission and where the caller stands with it today.
/// </summary>
/// <param name="Mission">The master data record, sent whole because the client carries no master data of its own.</param>
/// <param name="Progress">What the caller's statistic reached today, which can run past the goal.</param>
/// <param name="Claimed">Whether the reward has already been paid out.</param>
[MessagePackObject]
public sealed record MissionItem(
    [property: Key(0)] Mission Mission,
    [property: Key(1)] int Progress,
    [property: Key(2)] bool Claimed);
