namespace SharpCampus.Server.Common.Configuration;

// GoTrue's discovery document carries absolute URLs of the address the host reaches it at, the
// signing-key endpoint included. A container fetches the document fine and then follows those URLs
// into itself, so every backchannel request bound for the issuer's address is rewritten to the one
// this server can actually reach.
public sealed class GoTrueBackchannelHandler(SupabaseOptions supabase, HttpMessageHandler inner)
    : DelegatingHandler(inner)
{
    private readonly Uri _issuer = new(supabase.IssuerUrl);
    private readonly Uri _reachable = new(supabase.Url);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri && uri.Host == _issuer.Host && uri.Port == _issuer.Port)
        {
            request.RequestUri = new UriBuilder(uri)
            {
                Scheme = _reachable.Scheme,
                Host = _reachable.Host,
                Port = _reachable.Port,
            }.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
