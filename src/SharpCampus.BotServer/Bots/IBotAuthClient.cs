namespace SharpCampus.BotServer.Bots;

// Everything a bot needs from the identity provider, which is only ever an access token for one fixed
// account. Behind an interface so the pool can be exercised without an auth server.
public interface IBotAuthClient
{
    Task<BotAuthResult> SignInAsync(string email, string password);

    Task<BotAuthResult> SignUpAsync(string email, string password);
}

public sealed record BotAuthResult(string? AccessToken, string? ErrorMessage);
