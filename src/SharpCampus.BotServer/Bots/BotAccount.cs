namespace SharpCampus.BotServer.Bots;

// One pooled identity. The token is cached across runs of the same slot, and the nickname is set on
// the profile once and then belongs to the account.
public sealed class BotAccount(int slot, string email, string nickname)
{
    public int Slot { get; } = slot;

    public string Email { get; } = email;

    public string Nickname { get; } = nickname;

    public string AccessToken { get; set; } = string.Empty;

    public bool Named { get; set; }
}
