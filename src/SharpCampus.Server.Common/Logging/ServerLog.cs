using Microsoft.Extensions.Logging;
using ZLogger;

namespace SharpCampus.Server.Common.Logging;

public static partial class ServerLog
{
    [ZLoggerMessage(LogLevel.Information, "Server {serverName} {version} started")]
    public static partial void ServerStarted(this ILogger logger, string serverName, string version);
}
