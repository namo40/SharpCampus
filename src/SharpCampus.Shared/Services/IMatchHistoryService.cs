using MagicOnion;
using SharpCampus.Shared.Dtos;

namespace SharpCampus.Shared.Services;

/// <summary>
/// The games settlement has already written down. Every method requires a bearer token in the
/// <c>authorization</c> request header.
/// </summary>
public interface IMatchHistoryService : IService<IMatchHistoryService>
{
    /// <summary>
    /// Reads the caller's own most recent games, newest first. The server decides how many come back
    /// and there is no way to ask for older ones.
    /// </summary>
    /// <returns>The caller's latest games. Empty until the caller has finished one.</returns>
    UnaryResult<MatchHistoryEntry[]> GetMyMatchesAsync();
}
