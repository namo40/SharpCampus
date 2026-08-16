using System.Resources;

namespace SharpCampus.Cli.Resources;

// What the command REPL says in its own right; everything a client of any shape needs is in
// Client.Common. Hand written rather than generated: MSBuild puts the strongly typed output in the
// intermediate folder, where the IDE's project model does not pick it up. Member names are the resource keys.
internal static class Strings
{
    private static readonly ResourceManager _resources =
        new("SharpCampus.Cli.Resources.Strings", typeof(Strings).Assembly);

    public static string RankUsage => Get(nameof(RankUsage));
    public static string ReplBanner => Get(nameof(ReplBanner));

    private static string Get(string name) => _resources.GetString(name)!;
}
