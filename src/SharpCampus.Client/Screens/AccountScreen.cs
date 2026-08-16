using SharpCampus.Client.Common;
using SharpCampus.Client.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Profiles;
using Spectre.Console;
using CommonStrings = SharpCampus.Client.Common.Resources.Strings;

namespace SharpCampus.Client.Screens;

// Who the stored session belongs to, and the three things that can be done about it. Linking is only
// on the menu while there is nothing to lose by it: an account with an address is already permanent.
internal sealed class AccountScreen(ApiGateway gateway) : Screen(gateway)
{
    protected override async Task<ScreenResult> ShowAsync()
    {
        while (true)
        {
            Ui.Begin(Strings.AccountTitle);

            var identity = await Gateway.Account.GetMyIdentityAsync();
            Describe(identity);

            switch (Ui.Choose(Strings.MenuPrompt, Choices(), Label))
            {
                case AccountChoice.Nickname:
                    await RenameAsync();
                    break;

                case AccountChoice.Link:
                    if (await LinkAsync())
                    {
                        return ScreenResult.SessionChanged;
                    }

                    break;

                case AccountChoice.LogOut:
                    ClientSession.Delete();
                    return ScreenResult.SignedOut;

                case AccountChoice.Back:
                    return ScreenResult.Back;
            }
        }
    }

    private void Describe(IdentityResponse identity)
    {
        // An anonymous account has no address to report, and the server passes that through as it is.
        var caller = string.IsNullOrEmpty(identity.Email) ? CommonStrings.GuestLabel : identity.Email;

        AnsiConsole.MarkupLineInterpolated($"[grey]{Strings.AccountEmailLabel}[/] [green]{caller}[/]");
        AnsiConsole.MarkupLineInterpolated($"[grey]{Strings.AccountUserIdLabel}[/] [grey]{identity.UserId}[/]");

        if (Gateway.Session.IsGuest)
        {
            Ui.Hint(Strings.GuestHint);
        }

        AnsiConsole.WriteLine();
    }

    private async Task RenameAsync()
    {
        var nickname = Ui.Ask(Strings.PromptNickname);

        // The server validates as well; checking here saves a round trip on an obvious typo.
        if (!NicknameRules.IsValid(nickname))
        {
            Ui.Error(Localization.Format(CommonStrings.NicknameRule, NicknameRules.MinLength, NicknameRules.MaxLength));
            Ui.Pause();
            return;
        }

        switch (await Gateway.Profile.UpdateNicknameAsync(nickname))
        {
            case NicknameUpdateResult.Updated:
                Ui.Info(Localization.Format(CommonStrings.NicknameUpdated, nickname));
                break;
            case NicknameUpdateResult.Duplicate:
                Ui.Warn(Localization.Format(CommonStrings.NicknameTaken, nickname));
                break;
            case NicknameUpdateResult.Invalid:
                Ui.Error(Localization.Format(CommonStrings.NicknameRejected, nickname));
                break;
        }

        Ui.Pause();
    }

    private async Task<bool> LinkAsync()
    {
        var email = Ui.Ask(Strings.PromptEmail);
        var password = Ui.AskSecret(Strings.PromptPassword);

        var link = await SupabaseAuthClient.LinkEmailAsync(Gateway.Session.AccessToken, email, password);
        if (link.ErrorMessage is not null)
        {
            Ui.Error(Localization.Format(CommonStrings.LinkFailed, link.ErrorMessage));
            Ui.Pause();
            return false;
        }

        // The token in hand was minted before the address existed and still claims to be anonymous,
        // so the session worth keeping is the one a fresh sign-in returns.
        var signIn = await SupabaseAuthClient.SignInAsync(email, password);
        if (signIn.AccessToken is null)
        {
            Ui.Error(Localization.Format(CommonStrings.LoginFailed, signIn.ErrorMessage));
            Ui.Pause();
            return false;
        }

        new ClientSession(signIn.AccessToken, email).Save();
        Ui.Info(Localization.Format(CommonStrings.Linked, email));
        Ui.Pause();

        return true;
    }

    private IReadOnlyList<AccountChoice> Choices() => Gateway.Session.IsGuest
        ? [AccountChoice.Nickname, AccountChoice.Link, AccountChoice.LogOut, AccountChoice.Back]
        : [AccountChoice.Nickname, AccountChoice.LogOut, AccountChoice.Back];

    private static string Label(AccountChoice choice) => choice switch
    {
        AccountChoice.Nickname => Strings.MenuChangeNickname,
        AccountChoice.Link => Strings.MenuLinkAccount,
        AccountChoice.LogOut => Strings.MenuLogOut,
        _ => Strings.MenuBack,
    };

    private enum AccountChoice
    {
        Nickname,
        Link,
        LogOut,
        Back,
    }
}
