namespace SharpCampus.Server.Common.Configuration;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    // Production note: point this at the hosted project through a K8s ConfigMap instead of a checked-in file.
    public string Url { get; set; } = "";

    // GoTrue is mounted under /auth/v1 and stamps that same absolute URL into the `iss` claim.
    public string Authority => $"{Url.TrimEnd('/')}/auth/v1";
}
