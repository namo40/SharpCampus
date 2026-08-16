using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SharpCampus.BotServer.Configuration;

namespace SharpCampus.BotServer.Bots;

// A bot signs in to Supabase Auth the same way the console client does, and for the same reason: the
// SharpCampus servers only ever see the access token it returns.
internal sealed class SupabaseBotAuthClient : IBotAuthClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _publishableKey;

    public SupabaseBotAuthClient(IOptions<BotServerOptions> options)
    {
        _http = new HttpClient { BaseAddress = new Uri(options.Value.SupabaseAuthUrl) };
        _publishableKey = options.Value.SupabasePublishableKey;
    }

    public Task<BotAuthResult> SignUpAsync(string email, string password)
        => PostCredentialsAsync("auth/v1/signup", email, password);

    public Task<BotAuthResult> SignInAsync(string email, string password)
        => PostCredentialsAsync("auth/v1/token?grant_type=password", email, password);

    public void Dispose() => _http.Dispose();

    private async Task<BotAuthResult> PostCredentialsAsync(string path, string email, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { email, password }, options: JsonSerializerOptions.Web),
        };

        // Supabase's gateway drops any call that carries no project key, sign-up included.
        request.Headers.Add("apikey", _publishableKey);

        HttpResponseMessage response;
        try
        {
            // Production note: a real client would time out, retry transient failures and keep the refresh token.
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return new BotAuthResult(null, $"Supabase is unreachable at {_http.BaseAddress}. Run: supabase start");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync();

            return response.IsSuccessStatusCode
                ? new BotAuthResult(JsonSerializer.Deserialize<SessionPayload>(body)!.AccessToken, null)
                : new BotAuthResult(null, JsonSerializer.Deserialize<ErrorPayload>(body)?.Message ?? body);
        }
    }

    private sealed record SessionPayload([property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record ErrorPayload([property: JsonPropertyName("msg")] string Message);
}
