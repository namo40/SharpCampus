using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharpCampus.Cli;

// The client talks to Supabase Auth directly; the SharpCampus servers only ever see the access token it returns.
internal static class SupabaseAuthClient
{
    // Values printed by `supabase start`. Every local stack ships the same demo publishable key.
    private const string BaseUrl = "http://127.0.0.1:54321";
    private const string PublishableKey = "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH";

    private static readonly HttpClient _http = new() { BaseAddress = new Uri(BaseUrl) };

    internal sealed record Result(string? AccessToken, string? ErrorMessage);

    public static Task<Result> SignUpAsync(string email, string password) =>
        PostCredentialsAsync("auth/v1/signup", email, password);

    public static Task<Result> SignInAsync(string email, string password) =>
        PostCredentialsAsync("auth/v1/token?grant_type=password", email, password);

    private static async Task<Result> PostCredentialsAsync(string path, string email, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { email, password }, options: JsonSerializerOptions.Web),
        };

        // Supabase's gateway drops any call that carries no project key, sign-up included.
        request.Headers.Add("apikey", PublishableKey);

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
            var body = await response.Content.ReadAsStringAsync();

            return response.IsSuccessStatusCode
                ? new Result(JsonSerializer.Deserialize<SessionPayload>(body)!.AccessToken, null)
                : new Result(null, JsonSerializer.Deserialize<ErrorPayload>(body)?.Message ?? body);
        }
    }

    private sealed record SessionPayload([property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record ErrorPayload([property: JsonPropertyName("msg")] string Message);
}
