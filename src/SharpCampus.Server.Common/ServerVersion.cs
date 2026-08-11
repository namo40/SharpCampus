using System.Reflection;

namespace SharpCampus.Server.Common;

public static class ServerVersion
{
    // Informational version of the host executable, e.g. "1.0.0+<commit>" produced by SourceLink.
    public static string Current { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0-unknown";
}
