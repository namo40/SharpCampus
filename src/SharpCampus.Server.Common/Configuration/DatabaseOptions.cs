namespace SharpCampus.Server.Common.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    // Production note: inject this from a K8s secret rather than a checked-in file, and point it at a
    // connection pooler (Supavisor on hosted Supabase) so pod restarts do not exhaust Postgres connections.
    public string ConnectionString { get; set; } = "";
}
