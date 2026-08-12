using MagicOnion;
using SharpCampus.Shared.Dtos;

namespace SharpCampus.Shared.Services;

/// <summary>
/// The duel queue. Every method requires a bearer token in the <c>authorization</c> request header,
/// and acts on the account that token belongs to.
/// </summary>
public interface IMatchmakingService : IService<IMatchmakingService>
{
    /// <summary>
    /// Joins the queue. Calling it again while queued or matched changes nothing and reports the state as it stands.
    /// </summary>
    /// <returns>Where the caller stands after the call.</returns>
    UnaryResult<MatchStatusResponse> EnqueueAsync();

    /// <summary>
    /// Leaves the queue. Does nothing once a match has already been made.
    /// </summary>
    /// <returns>A task that completes once the caller is out of the queue.</returns>
    UnaryResult CancelAsync();

    /// <summary>
    /// Reads where the caller stands. Clients poll this while queued.
    /// </summary>
    /// <returns>Where the caller stands, with the entry ticket once one exists.</returns>
    UnaryResult<MatchStatusResponse> GetStatusAsync();
}
