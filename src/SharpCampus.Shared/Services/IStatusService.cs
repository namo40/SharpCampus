using MagicOnion;
using SharpCampus.Shared.Dtos;

namespace SharpCampus.Shared.Services;

/// <summary>
/// Reports which server a client is talking to. Every SharpCampus server exposes this service.
/// </summary>
public interface IStatusService : IService<IStatusService>
{
    /// <summary>
    /// Gets the status of the server that handled this call.
    /// </summary>
    /// <returns>The name, version and current UTC time of the responding server.</returns>
    UnaryResult<StatusResponse> GetStatusAsync();
}
