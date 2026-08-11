using Microsoft.Extensions.Logging;
using ZLogger;

namespace SharpCampus.Server.Common.Logging;

public static class LoggingBuilderExtensions
{
    // Production note: log shipping/rotation belongs to the container platform, so stdout is the only sink here.
    public static ILoggingBuilder AddSharpCampusLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddZLoggerConsole(options => options.UseJsonFormatter());

        // The request start/finish log states of this category read the live HttpContext when their
        // parameters are enumerated, and the json formatter enumerates them on ZLogger's background
        // writer thread — concurrently with the request. That data race corrupts the request's feature
        // cache (observed as a NullReferenceException inside routing under load), so these logs must
        // never reach an asynchronous sink. Same failure class as
        // https://github.com/dotnet/aspnetcore/issues/41924.
        logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

        return logging;
    }
}
