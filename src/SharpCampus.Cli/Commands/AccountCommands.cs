using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Cli.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class AccountCommands
{
    private const string ApiServerAddress = "http://localhost:5001";
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

        using var channel = GrpcChannel.ForAddress(ApiServerAddress);
        var client = MagicOnionClient.Create<IAccountService>(channel)
            .WithHeaders(new Metadata { { "authorization", $"Bearer {session.AccessToken}" } });

        try
        {
            var identity = await client.GetMyIdentityAsync();
            var profile = await client.GetMyProfileAsync();

            AnsiConsole.MarkupLineInterpolated($"[green]{identity.Email}[/] [grey]{identity.UserId}[/]");
            AnsiConsole.MarkupLineInterpolated(
                $"[green]{profile.Nickname}[/] [grey]{Localization.Format(Strings.ProfileSummary, profile.Coins.AsPrimitive(), profile.Rating.AsPrimitive(), Strings.SkinName(profile.EquippedSkinId))}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Strings.SessionExpired}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, ApiServerLabel, ApiServerAddress)}[/]");
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

        using var channel = GrpcChannel.ForAddress(ApiServerAddress);
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
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, ApiServerLabel, ApiServerAddress)}[/]");
        }
    }
}
