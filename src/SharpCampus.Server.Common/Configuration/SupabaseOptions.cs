namespace SharpCampus.Server.Common.Configuration;

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    // Production note: point this at the hosted project through a K8s ConfigMap instead of a checked-in file.
    public string Url { get; set; } = "";

    // Only set where the address this server reaches GoTrue at is not the one GoTrue puts in its tokens,
    // which is what a container behind a host gateway runs into. Empty leaves the two identical.
    public string IssuerUrl { get; set; } = "";

    // GoTrue is mounted under /auth/v1 and stamps that same absolute URL into the `iss` claim.
    public string Authority => $"{Url.TrimEnd('/')}/auth/v1";

    public string Issuer => $"{(IssuerUrl.Length == 0 ? Url : IssuerUrl).TrimEnd('/')}/auth/v1";
}
