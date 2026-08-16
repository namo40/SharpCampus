using System.Globalization;
using Microsoft.Extensions.Options;
using SharpCampus.BotServer.Configuration;

namespace SharpCampus.BotServer.Bots;

// The fixed set of accounts this server plays as. One bot per slot at a time: two matches signed in as
// the same account would take each other's place in the queue.
public sealed class BotAccountPool(
    IBotAuthClient auth,
    IBotProfileClient profiles,
    IOptions<BotServerOptions> options,
    ILogger<BotAccountPool> logger)
{
    private readonly Lock _gate = new();
    private readonly BotServerOptions _options = options.Value;
    private readonly BotAccount[] _accounts = CreateAccounts(options.Value);
    private readonly bool[] _leased = new bool[options.Value.AccountPoolSize];

    public int Size => _accounts.Length;

    // Taking a slot is all a summon may do: its caller pairs matches under a short lock and cannot wait
    // on an auth round trip. Null when every account is out, which is what a summon turns into Exhausted.
    public BotAccount? Lease()
    {
        lock (_gate)
        {
            return TakeFree();
        }
    }

    // Signing in belongs to the bot task instead, off the summon's answer. The token is cached for the
    // life of the process, so only the first run of a slot pays for it.
    public Task<bool> EnsureSignedInAsync(BotAccount account)
        => account.AccessToken.Length > 0 ? Task.FromResult(true) : SignInAsync(account);

    public void Return(BotAccount account)
    {
        lock (_gate)
        {
            _leased[account.Slot] = false;
        }
    }

    // Supabase access tokens expire, and a pooled account can sit on one for hours. Whoever hears
    // Unauthenticated asks for a fresh token and tries again, once.
    public Task<bool> RefreshAsync(BotAccount account)
    {
        account.AccessToken = string.Empty;
        return SignInAsync(account);
    }

    private static BotAccount[] CreateAccounts(BotServerOptions options)
    {
        var accounts = new BotAccount[options.AccountPoolSize];
        for (var slot = 0; slot < accounts.Length; slot++)
        {
            var number = slot + 1;
            accounts[slot] = new BotAccount(
                slot,
                string.Format(CultureInfo.InvariantCulture, options.EmailPattern, number),
                string.Format(CultureInfo.InvariantCulture, options.NicknamePattern, number));
        }

        return accounts;
    }

    private BotAccount? TakeFree()
    {
        for (var slot = 0; slot < _accounts.Length; slot++)
        {
            if (!_leased[slot])
            {
                _leased[slot] = true;
                return _accounts[slot];
            }
        }

        return null;
    }

    private async Task<bool> SignInAsync(BotAccount account)
    {
        var result = await auth.SignInAsync(account.Email, _options.Password);

        // A fresh Supabase stack has no bot accounts in it yet, and creating one is a sign-up away.
        if (result.AccessToken is null)
        {
            await auth.SignUpAsync(account.Email, _options.Password);
            result = await auth.SignInAsync(account.Email, _options.Password);
        }

        if (result.AccessToken is null)
        {
            logger.BotAuthFailed(account.Nickname, result.ErrorMessage ?? string.Empty);
            return false;
        }

        account.AccessToken = result.AccessToken;
        account.Named = account.Named || await profiles.RenameAsync(account.AccessToken, account.Nickname);
        return true;
    }
}
