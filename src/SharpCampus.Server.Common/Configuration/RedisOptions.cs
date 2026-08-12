namespace SharpCampus.Server.Common.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    // Production note: point this at the in-cluster Redis through a K8s ConfigMap, and move any
    // credentials into a Secret rather than a checked-in file.
    public string ConnectionString { get; set; } = "";
}
