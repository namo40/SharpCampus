using MagicOnion;
using MagicOnion.Server;
using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Services;

namespace SharpCampus.Server.Common.Services;

public sealed class StatusService(IOptions<ServerOptions> options) : ServiceBase<IStatusService>, IStatusService
{
    public UnaryResult<StatusResponse> GetStatusAsync() =>
        UnaryResult.FromResult(new StatusResponse(options.Value.Name, ServerVersion.Current, DateTime.UtcNow));
}
