using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpCampus.Client.Common;

// The client talks to Supabase Auth directly; the SharpCampus servers only ever see the access token it returns.
public static class SupabaseAuthClient
{
    // Values printed by `supabase start`. Every local stack ships the same demo publishable key.
    private const string BaseUrl = "http://127.0.0.1:54321";
    private const string PublishableKey = "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH";

    private static readonly HttpClient _http = new() { BaseAddress = new Uri(BaseUrl) };

    public sealed record Result(string? AccessToken, string? ErrorMessage);

    public static Task<Result> SignUpAsync(string email, string password) =>
        SendAsync(HttpMethod.Post, "auth/v1/signup", new { email, password });

    public static Task<Result> SignInAsync(string email, string password) =>
        SendAsync(HttpMethod.Post, "auth/v1/token?grant_type=password", new { email, password });

    // Production note: every launch that never links leaves an account behind, so a real service puts a
    // captcha and a rate limit in front of this route and sweeps the abandoned rows on a schedule.
    public static Task<Result> SignUpGuestAsync() =>
        SendAsync(HttpMethod.Post, "auth/v1/signup", new { });

    // One update carries both, and the user id stays what it was, so the profile the guest already built
    // remains theirs. The reply describes the user rather than a session, so it carries no token: the
    // caller signs in again to get one that no longer claims to be anonymous.
    public static Task<Result> LinkEmailAsync(string accessToken, string email, string password) =>
        SendAsync(HttpMethod.Put, "auth/v1/user", new { email, password }, accessToken);

    private static async Task<Result> SendAsync(HttpMethod method, string path, object body, string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, body.GetType(), options: JsonSerializerOptions.Web),
        };

        // Supabase's gateway drops any call that carries no project key, sign-up included.
        request.Headers.Add("apikey", PublishableKey);

        if (accessToken is not null)
        {
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
        }

        HttpResponseMessage response;
        try
        {
            // Production note: a real client would time out, retry transient failures and keep the refresh token.
            response = await _http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return new Result(null, $"Supabase is unreachable at {BaseUrl}. Run: supabase start");
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync();

            return response.IsSuccessStatusCode
                ? new Result(JsonSerializer.Deserialize<SessionPayload>(payload)!.AccessToken, null)
                : new Result(null, JsonSerializer.Deserialize<ErrorPayload>(payload)?.Message ?? payload);
        }
    }

    private sealed record SessionPayload([property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record ErrorPayload([property: JsonPropertyName("msg")] string Message);
}
