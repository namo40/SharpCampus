using SharpCampus.Server.Common.Configuration;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class GoTrueBackchannelHandlerTests
{
    private readonly CapturingHandler _inner = new();

    private HttpMessageInvoker CreateInvoker() => new(new GoTrueBackchannelHandler(
        new SupabaseOptions
        {
            Url = "http://host.docker.internal:54321",
            IssuerUrl = "http://127.0.0.1:54321",
        },
        _inner));

    [Fact]
    public async Task FetchAimedAtTheIssuer_GoesToTheAddressThisServerCanReach()
    {
        await CreateInvoker().SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:54321/auth/v1/.well-known/jwks.json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new Uri("http://host.docker.internal:54321/auth/v1/.well-known/jwks.json"),
            _inner.Seen);
    }

    [Fact]
    public async Task FetchAimedAnywhereElse_IsLeftAlone()
    {
        var elsewhere = new Uri("http://host.docker.internal:54321/auth/v1/.well-known/openid-configuration");

        await CreateInvoker().SendAsync(
            new HttpRequestMessage(HttpMethod.Get, elsewhere),
            TestContext.Current.CancellationToken);

        Assert.Equal(elsewhere, _inner.Seen);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Seen = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
