using SharpCampus.Client.Common;
using SharpCampus.Client.Resources;
using CommonStrings = SharpCampus.Client.Common.Resources.Strings;

namespace SharpCampus.Client.Screens;

// The only screen that runs without a session, and the only one that can make one.
internal sealed class TitleScreen
{
    private static readonly TitleChoice[] _choices =
        [TitleChoice.LogIn, TitleChoice.SignUp, TitleChoice.Guest, TitleChoice.Exit];

    // Null when the player would rather not play at all.
    public async Task<ClientSession?> RunAsync()
    {
        while (true)
        {
            Ui.Banner();

            switch (Ui.Choose(Strings.TitleMenuTitle, _choices, Label))
            {
                case TitleChoice.LogIn:
                    if (await LogInAsync() is { } signedIn)
                    {
                        return signedIn;
                    }

                    break;

                case TitleChoice.SignUp:
                    if (await SignUpAsync() is { } registered)
                    {
                        return registered;
                    }

                    break;

                case TitleChoice.Guest:
                    if (await SignUpGuestAsync() is { } guest)
                    {
                        return guest;
                    }

                    break;

                case TitleChoice.Exit:
                    return null;
            }
        }
    }

    private static async Task<ClientSession?> LogInAsync()
    {
        var email = Ui.Ask(Strings.PromptEmail);
        var password = Ui.AskSecret(Strings.PromptPassword);

        var result = await SupabaseAuthClient.SignInAsync(email, password);

        return result.AccessToken is null
            ? Refused(Localization.Format(CommonStrings.LoginFailed, result.ErrorMessage))
            : Keep(result.AccessToken, email);
    }

    private static async Task<ClientSession?> SignUpAsync()
    {
        var email = Ui.Ask(Strings.PromptEmail);
        var password = Ui.AskSecret(Strings.PromptPassword);

        var result = await SupabaseAuthClient.SignUpAsync(email, password);

        return result.AccessToken is null
            ? Refused(Localization.Format(CommonStrings.SignUpFailed, result.ErrorMessage))
            : Keep(result.AccessToken, email);
    }

    // An account with no address on it: the lobby says so, and the account screen is where it stops
    // being one.
    private static async Task<ClientSession?> SignUpGuestAsync()
    {
        var result = await SupabaseAuthClient.SignUpGuestAsync();

        return result.AccessToken is null
            ? Refused(Localization.Format(CommonStrings.SignUpFailed, result.ErrorMessage))
            : Keep(result.AccessToken, null);
    }

    private static ClientSession Keep(string accessToken, string? email)
    {
        var session = new ClientSession(accessToken, email);
        session.Save();

        return session;
    }

    private static ClientSession? Refused(string message)
    {
        Ui.Error(message);
        Ui.Pause();

        return null;
    }

    private static string Label(TitleChoice choice) => choice switch
    {
        TitleChoice.LogIn => Strings.MenuLogIn,
        TitleChoice.SignUp => Strings.MenuSignUp,
        TitleChoice.Guest => Strings.MenuPlayAsGuest,
        _ => Strings.MenuExit,
    };

    private enum TitleChoice
    {
        LogIn,
        SignUp,
        Guest,
        Exit,
    }
}
