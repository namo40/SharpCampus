using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Client.Common;
using SharpCampus.Client.Common.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class AccountCommands
{
    private const string ApiServerLabel = "ApiServer";

    /// <summary>Creates a Supabase account and signs in with it.</summary>
    /// <param name="email">Email address to register.</param>
    /// <param name="password">Password for the new account.</param>
    [Command("signup")]
    public async Task SignUpAsync([Argument] string email, [Argument] string password)
    {
        var result = await SupabaseAuthClient.SignUpAsync(email, password);
        if (result.AccessToken is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.SignUpFailed, result.ErrorMessage)}[/]");
            return;
        }

        new ClientSession(result.AccessToken, email).Save();
        AnsiConsole.MarkupLineInterpolated($"[green]{Localization.Format(Strings.SignedUp, email)}[/]");
    }

    /// <summary>Creates an anonymous account, so a player can start without an address to give.</summary>
    [Command("guest")]
    public async Task SignUpGuestAsync()
    {
        var result = await SupabaseAuthClient.SignUpGuestAsync();
        if (result.AccessToken is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.SignUpFailed, result.ErrorMessage)}[/]");
            return;
        }

        new ClientSession(result.AccessToken, null).Save();
        AnsiConsole.MarkupLineInterpolated($"[green]{Strings.GuestSignedUp}[/]");
    }

    /// <summary>Turns the guest account in the stored session into a permanent one.</summary>
    /// <param name="email">Email address to attach.</param>
    /// <param name="password">Password to set on the account.</param>
    [Command("link")]
    public async Task LinkEmailAsync([Argument] string email, [Argument] string password)
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        if (!session.IsGuest)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Localization.Format(Strings.AlreadyFullAccount, session.Email)}[/]");
            return;
        }

        var link = await SupabaseAuthClient.LinkEmailAsync(session.AccessToken, email, password);
        if (link.ErrorMessage is not null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.LinkFailed, link.ErrorMessage)}[/]");
            return;
        }

        // The token in hand was minted before the address existed and still claims to be anonymous,
        // so the session worth keeping is the one a fresh sign-in returns.
        var signIn = await SupabaseAuthClient.SignInAsync(email, password);
        if (signIn.AccessToken is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.LoginFailed, signIn.ErrorMessage)}[/]");
            return;
        }

        new ClientSession(signIn.AccessToken, email).Save();
        AnsiConsole.MarkupLineInterpolated($"[green]{Localization.Format(Strings.Linked, email)}[/]");
    }

    /// <summary>Signs in to Supabase and stores the session for later commands.</summary>
    /// <param name="email">Email address of the account.</param>
    /// <param name="password">Password of the account.</param>
    [Command("login")]
    public async Task LogInAsync([Argument] string email, [Argument] string password)
    {
        var result = await SupabaseAuthClient.SignInAsync(email, password);
        if (result.AccessToken is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.LoginFailed, result.ErrorMessage)}[/]");
            return;
        }

        new ClientSession(result.AccessToken, email).Save();
        AnsiConsole.MarkupLineInterpolated($"[green]{Localization.Format(Strings.LoggedIn, email)}[/]");
    }

    /// <summary>Discards the stored session.</summary>
    [Command("logout")]
    public void LogOut()
    {
        if (ClientSession.Delete())
        {
            AnsiConsole.MarkupLineInterpolated($"[green]{Strings.LoggedOut}[/]");
            return;
        }

        AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedInShort}[/]");
    }

    /// <summary>Asks the ApiServer which account the stored session belongs to.</summary>
    [Command("whoami")]
    public async Task WhoAmIAsync()
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        using var channel = GrpcChannel.ForAddress(ClientEndpoints.ApiServer);
        var client = MagicOnionClient.Create<IAccountService>(channel)
            .WithHeaders(new Metadata { { "authorization", $"Bearer {session.AccessToken}" } });

        try
        {
            var identity = await client.GetMyIdentityAsync();
            var profile = await client.GetMyProfileAsync();

            // An anonymous account has no address to report, and the server passes that through as it is.
            var caller = string.IsNullOrEmpty(identity.Email) ? Strings.GuestLabel : identity.Email;

            AnsiConsole.MarkupLineInterpolated($"[green]{caller}[/] [grey]{identity.UserId}[/]");
            AnsiConsole.MarkupLineInterpolated(
                $"[green]{profile.Nickname}[/] [grey]{Localization.Format(Strings.ProfileSummary, profile.Coins.AsPrimitive(), profile.Rating.AsPrimitive(), Strings.SkinName(profile.EquippedSkinId))}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Strings.SessionExpired}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, ApiServerLabel, ClientEndpoints.ApiServer)}[/]");
        }
    }

    /// <summary>Renames the profile the stored session belongs to.</summary>
    /// <param name="nickname">New nickname: 2 to 16 letters, digits or underscores.</param>
    [Command("nickname")]
    public async Task SetNicknameAsync([Argument] string nickname)
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        // The server validates as well; checking here saves a round trip on an obvious typo.
        if (!NicknameRules.IsValid(nickname))
        {
            AnsiConsole.MarkupLineInterpolated(
                $"[red]{Localization.Format(Strings.NicknameRule, NicknameRules.MinLength, NicknameRules.MaxLength)}[/]");
            return;
        }

        using var channel = GrpcChannel.ForAddress(ClientEndpoints.ApiServer);
        var client = MagicOnionClient.Create<IProfileService>(channel)
            .WithHeaders(new Metadata { { "authorization", $"Bearer {session.AccessToken}" } });

        try
        {
            switch (await client.UpdateNicknameAsync(nickname))
            {
                case NicknameUpdateResult.Updated:
                    AnsiConsole.MarkupLineInterpolated($"[green]{Localization.Format(Strings.NicknameUpdated, nickname)}[/]");
                    break;
                case NicknameUpdateResult.Duplicate:
                    AnsiConsole.MarkupLineInterpolated($"[yellow]{Localization.Format(Strings.NicknameTaken, nickname)}[/]");
                    break;
                case NicknameUpdateResult.Invalid:
                    AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.NicknameRejected, nickname)}[/]");
                    break;
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Strings.SessionExpired}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, ApiServerLabel, ClientEndpoints.ApiServer)}[/]");
        }
    }
}
