using MagicOnion;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Services;

/// <summary>
/// The daily missions matches count towards, and the coins completing them pays. Every method requires a
/// bearer token in the <c>authorization</c> request header.
/// </summary>
public interface IMissionService : IService<IMissionService>
{
    /// <summary>
    /// Gets today's missions, each merged with what the caller has done towards it. A day here is a UTC
    /// date, so every account's missions turn over at the same moment.
    /// </summary>
    /// <returns>Every mission master data holds, in master data order, with the caller's progress.</returns>
    UnaryResult<MissionItem[]> GetMissionsAsync();

    /// <summary>
    /// Claims the reward of a mission the caller completed today. A second claim pays nothing.
    /// </summary>
    /// <param name="missionId">Mission to claim.</param>
    /// <returns>Whether the reward was paid, and why not when it was not.</returns>
    UnaryResult<MissionClaimResult> ClaimAsync(MissionId missionId);
}
