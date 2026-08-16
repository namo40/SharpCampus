using System.Globalization;
using SharpCampus.Client.Resources;
using SharpCampus.Shared.Dtos;
using Spectre.Console;
using CommonStrings = SharpCampus.Client.Common.Resources.Strings;

namespace SharpCampus.Client.Screens;

// Read only: the server keeps the record and there is nothing here to change about it.
internal sealed class HistoryScreen(ApiGateway gateway) : Screen(gateway)
{
    // A fixed pattern rather than the machine's: the language is chosen at startup while the date
    // format would still follow the OS, which reads as two different locales in one table.
    private const string PlayedAtFormat = "yyyy-MM-dd HH:mm";

    protected override async Task<ScreenResult> ShowAsync()
    {
        Ui.Begin(Strings.MenuHistory);
        Render(await Gateway.History.GetMyMatchesAsync());
        Ui.WaitForBack();

        return ScreenResult.Back;
    }

    private static void Render(MatchHistoryEntry[] entries)
    {
        if (entries.Length == 0)
        {
            Ui.Warn(CommonStrings.HistoryEmpty);
            return;
        }

        Ui.Hint(CommonStrings.HistoryHeader);

        var table = new Table();
        table.AddColumn(CommonStrings.HistoryColumnDate);
        table.AddColumn(CommonStrings.HistoryColumnOpponent);
        table.AddColumn(CommonStrings.HistoryColumnResult);
        table.AddColumn(CommonStrings.HistoryColumnReason);
        table.AddColumn(CommonStrings.HistoryColumnRating);
        table.AddColumn(CommonStrings.HistoryColumnCoins);

        foreach (var entry in entries)
        {
            table.AddRow(
                new Markup(entry.PlayedAt.ToLocalTime().ToString(PlayedAtFormat, CultureInfo.InvariantCulture)),
                new Markup(Opponent(entry)),
                new Markup(Outcome(entry.Outcome)),
                new Markup($"[grey]{Markup.Escape(Reason(entry.EndReason))}[/]"),
                new Markup(RatingChange(entry)),
                new Markup($"{entry.CoinsAwarded.AsPrimitive()}"));
        }

        AnsiConsole.Write(table);
    }

    private static string Opponent(MatchHistoryEntry entry) => entry.OpponentNickname.Length == 0
        ? $"[grey]{Markup.Escape(CommonStrings.HistoryUnknownOpponent)}[/]"
        : Markup.Escape(entry.OpponentNickname);

    // The stored strings are the contract rather than an enum, so a value this build has never heard
    // of is shown as it was written down instead of being hidden.
    private static string Outcome(string outcome) => outcome switch
    {
        "win" => $"[green]{CommonStrings.HistoryOutcomeWin}[/]",
        "loss" => $"[red]{CommonStrings.HistoryOutcomeLoss}[/]",
        "draw" => $"[grey]{CommonStrings.HistoryOutcomeDraw}[/]",
        _ => Markup.Escape(outcome),
    };

    private static string Reason(string reason) => reason switch
    {
        "top_out" => CommonStrings.ReasonTopOut,
        "forfeit" => CommonStrings.ReasonForfeit,
        "disconnect" => CommonStrings.ReasonDisconnect,
        _ => reason,
    };

    private static string RatingChange(MatchHistoryEntry entry)
    {
        var before = entry.RatingBefore.AsPrimitive();
        var after = entry.RatingAfter.AsPrimitive();
        var delta = after - before;

        var color = delta switch
        {
            > 0 => "green",
            < 0 => "red",
            _ => "grey",
        };

        return $"{before} → {after} [{color}]{delta.ToString("+#;-#;0", CultureInfo.InvariantCulture)}[/]";
    }
}
