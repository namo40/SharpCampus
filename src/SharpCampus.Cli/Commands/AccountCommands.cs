using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class AccountCommands
{
    private const string ApiServerAddress = "http://localhost:5001";

    /// <summary>Creates a Supabase account and signs in with it.</summary>
    /// <param name="email">Email address to register.</param>
    /// <param name="password">Password for the new account.</param>
    [Command("signup")]
    public async Task SignUpAsync([Argument] string email, [Argument] string password)
    {
        var result = await SupabaseAuthClient.SignUpAsync(email, password);
        if (result.AccessToken is null)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Sign-up failed: {result.ErrorMessage}[/]");
            return;
        }

        new ClientSession(result.AccessToken, email).Save();
        AnsiConsole.MarkupLineInterpolated($"[green]Signed up as {email}.[/]");
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
            AnsiConsole.MarkupLineInterpolated($"[red]Login failed: {result.ErrorMessage}[/]");
            return;
        }

        new ClientSession(result.AccessToken, email).Save();
        AnsiConsole.MarkupLineInterpolated($"[green]Logged in as {email}.[/]");
    }

    /// <summary>Discards the stored session.</summary>
    [Command("logout")]
    public void LogOut() =>
        AnsiConsole.MarkupLine(ClientSession.Delete() ? "[green]Logged out.[/]" : "[yellow]Not logged in.[/]");

    /// <summary>Asks the ApiServer which account the stored session belongs to.</summary>
    [Command("whoami")]
    public async Task WhoAmIAsync()
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLine("[yellow]Not logged in. Run: login <email> <password>[/]");
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
                $"[green]{profile.Nickname}[/] [grey]{profile.Coins.AsPrimitive()} coins, rating {profile.Rating.AsPrimitive()}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLine("[red]Session expired or invalid. Run: login <email> <password>[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]ApiServer: unreachable at {ApiServerAddress}[/]");
        }
    }

    /// <summary>Renames the profile the stored session belongs to.</summary>
    /// <param name="nickname">New nickname: 2 to 16 letters, digits or underscores.</param>
    [Command("nickname")]
    public async Task SetNicknameAsync([Argument] string nickname)
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLine("[yellow]Not logged in. Run: login <email> <password>[/]");
            return;
        }

        // The server validates as well; checking here saves a round trip on an obvious typo.
        if (!NicknameRules.IsValid(nickname))
        {
            AnsiConsole.MarkupLine($"[red]Nicknames are {NicknameRules.MinLength} to {NicknameRules.MaxLength} letters, digits or underscores.[/]");
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
                    AnsiConsole.MarkupLineInterpolated($"[green]Nickname is now {nickname}.[/]");
                    break;
                case NicknameUpdateResult.Duplicate:
                    AnsiConsole.MarkupLineInterpolated($"[yellow]{nickname} is already taken.[/]");
                    break;
                case NicknameUpdateResult.Invalid:
                    AnsiConsole.MarkupLineInterpolated($"[red]The server rejected {nickname}.[/]");
                    break;
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLine("[red]Session expired or invalid. Run: login <email> <password>[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]ApiServer: unreachable at {ApiServerAddress}[/]");
        }
    }
}
