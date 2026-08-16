using SharpCampus.Client.Common;
using SharpCampus.Client.Resources;
using Spectre.Console;
using CommonStrings = SharpCampus.Client.Common.Resources.Strings;

namespace SharpCampus.Client;

// Every screen draws through here, so the app has one idea of what a title, a menu and a prompt look
// like, and one place that knows the console is driven by arrow keys rather than typed commands.
internal static class Ui
{
    private const string AppName = "SharpCampus";
    private const string ApiServerLabel = "ApiServer";

    private static readonly RetryChoice[] _retryChoices = [RetryChoice.Retry, RetryChoice.Exit];

    public static void Banner()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText(AppName).LeftJustified().Color(Color.Green));
        AnsiConsole.MarkupLineInterpolated($"[grey]{Strings.AppTagline}[/]");
        AnsiConsole.WriteLine();
    }

    public static void Begin(string title)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule($"[green]{Markup.Escape(title)}[/]").LeftJustified());
        AnsiConsole.WriteLine();
    }

    public static T Choose<T>(string title, IReadOnlyList<T> choices, Func<T, string> label)
        where T : notnull =>
        AnsiConsole.Prompt(new SelectionPrompt<T>()
            .Title(Markup.Escape(title))
            .UseConverter(choice => Markup.Escape(label(choice)))
            .AddChoices(choices));

    // A screen that only reports still leaves the same way every other one does.
    public static void WaitForBack() =>
        Choose<string>(Strings.MenuPrompt, [Strings.MenuBack], static choice => choice);

    public static string Ask(string prompt) =>
        AnsiConsole.Prompt(new TextPrompt<string>($"{Markup.Escape(prompt)}:"));

    public static string AskSecret(string prompt) =>
        AnsiConsole.Prompt(new TextPrompt<string>($"{Markup.Escape(prompt)}:").Secret());

    public static void Info(string text) => AnsiConsole.MarkupLineInterpolated($"[green]{text}[/]");

    public static void Hint(string text) => AnsiConsole.MarkupLineInterpolated($"[grey]{text}[/]");

    public static void Warn(string text) => AnsiConsole.MarkupLineInterpolated($"[yellow]{text}[/]");

    public static void Error(string text) => AnsiConsole.MarkupLineInterpolated($"[red]{text}[/]");

    public static void Pause()
    {
        AnsiConsole.WriteLine();
        Hint(Strings.PressAnyKey);
        Console.ReadKey(intercept: true);
    }

    public static string UnreachableMessage() =>
        Localization.Format(CommonStrings.ServerUnreachable, ApiServerLabel, ClientEndpoints.ApiServer);

    // A server that is not up is the one failure nothing here can work around, so it is put to the
    // player as a choice instead of dropping them onto a screen with no data on it.
    public static bool RetryRequested()
    {
        AnsiConsole.Clear();
        Error(UnreachableMessage());

        return Choose(Strings.MenuPrompt, _retryChoices, Label) == RetryChoice.Retry;
    }

    private static string Label(RetryChoice choice) => choice switch
    {
        RetryChoice.Retry => Strings.MenuRetry,
        _ => Strings.MenuExit,
    };

    private enum RetryChoice
    {
        Retry,
        Exit,
    }
}

// A menu row: what it reads as, and what picking it means. Lets a list of server data carry a Back
// item without the data type needing a spare value to stand for it.
internal sealed record Choice<T>(string Label, T Value);
