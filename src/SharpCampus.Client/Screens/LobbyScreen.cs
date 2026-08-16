using Grpc.Core;
using SharpCampus.Client.Common;
using SharpCampus.Client.Duel;
using SharpCampus.Client.Resources;
using SharpCampus.Shared.Dtos;
using Spectre.Console;
using CommonStrings = SharpCampus.Client.Common.Resources.Strings;

namespace SharpCampus.Client.Screens;

// Where a signed-in player comes back to. The profile is read again on every pass, so coins a match
// or a mission just paid are on screen the moment the screen is.
internal sealed class LobbyScreen(ApiGateway gateway)
{
    private static readonly LobbyChoice[] _choices =
    [
        LobbyChoice.Duel,
        LobbyChoice.Shop,
        LobbyChoice.Missions,
        LobbyChoice.Rankings,
        LobbyChoice.History,
        LobbyChoice.Account,
        LobbyChoice.Exit,
    ];

    public async Task<AppFlow> RunAsync()
    {
        while (true)
        {
            Ui.Begin(Strings.LobbyTitle);

            ProfileResponse profile;
            try
            {
                profile = await gateway.Account.GetMyProfileAsync();
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
            {
                ClientSession.Delete();
                Ui.Error(Strings.SessionEndedNotice);
                Ui.Pause();
                return AppFlow.Continue;
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
            {
                if (!Ui.RetryRequested())
                {
                    return AppFlow.Exit;
                }

                continue;
            }

            Header(profile);

            var choice = Ui.Choose(Strings.MenuPrompt, _choices, Label);
            if (choice == LobbyChoice.Exit)
            {
                return AppFlow.Exit;
            }

            // Both a discarded and a replaced session invalidate this gateway, and the app rebuilds
            // from what is stored either way.
            if (await Open(choice).RunAsync() is not ScreenResult.Back)
            {
                return AppFlow.Continue;
            }
        }
    }

    private Screen Open(LobbyChoice choice) => choice switch
    {
        LobbyChoice.Duel => new DuelFlow(gateway),
        LobbyChoice.Shop => new ShopScreen(gateway),
        LobbyChoice.Missions => new MissionScreen(gateway),
        LobbyChoice.Rankings => new RankingScreen(gateway),
        LobbyChoice.History => new HistoryScreen(gateway),
        _ => new AccountScreen(gateway),
    };

    private void Header(ProfileResponse profile)
    {
        var name = gateway.Session.IsGuest
            ? $"{profile.Nickname} {CommonStrings.GuestLabel}"
            : profile.Nickname;

        AnsiConsole.MarkupLineInterpolated($"[bold green]{name}[/]");
        AnsiConsole.MarkupLineInterpolated($"[grey]{Localization.Format(
            CommonStrings.ProfileSummary,
            profile.Coins.AsPrimitive(),
            profile.Rating.AsPrimitive(),
            CommonStrings.SkinName(profile.EquippedSkinId))}[/]");
        AnsiConsole.WriteLine();
    }

    private static string Label(LobbyChoice choice) => choice switch
    {
        LobbyChoice.Duel => Strings.MenuDuel,
        LobbyChoice.Shop => Strings.MenuShop,
        LobbyChoice.Missions => Strings.MenuMissions,
        LobbyChoice.Rankings => Strings.MenuRankings,
        LobbyChoice.History => Strings.MenuHistory,
        LobbyChoice.Account => Strings.MenuAccount,
        _ => Strings.MenuExit,
    };

    private enum LobbyChoice
    {
        Duel,
        Shop,
        Missions,
        Rankings,
        History,
        Account,
        Exit,
    }
}
