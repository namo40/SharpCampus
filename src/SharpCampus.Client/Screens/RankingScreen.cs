using SharpCampus.Client.Resources;
using SharpCampus.Shared.Dtos;
using Spectre.Console;
using CommonStrings = SharpCampus.Client.Common.Resources.Strings;

namespace SharpCampus.Client.Screens;

// The same two boards the server keeps, picked from a menu rather than named on a command line.
internal sealed class RankingScreen(ApiGateway gateway) : Screen(gateway)
{
    protected override async Task<ScreenResult> ShowAsync()
    {
        while (true)
        {
            Ui.Begin(Strings.RankingsTitle);

            if (Ui.Choose(Strings.RankSelectBoard, Boards(), choice => choice.Label).Value is not { } kind)
            {
                return ScreenResult.Back;
            }

            Ui.Begin(Title(kind));
            Render(kind, await Gateway.Leaderboard.GetLeaderboardAsync(kind));
            Ui.WaitForBack();
        }
    }

    private static void Render(LeaderboardKind kind, LeaderboardView view)
    {
        if (view.Top.Length == 0)
        {
            Ui.Warn(CommonStrings.RankEmpty);
            return;
        }

        var table = new Table();
        table.AddColumn(CommonStrings.RankColumnRank);
        table.AddColumn(CommonStrings.RankColumnPlayer);
        table.AddColumn(ScoreColumn(kind));

        foreach (var entry in view.Top)
        {
            AddRow(table, entry, entry.Rank == view.Me?.Rank);
        }

        // A caller further down than the places listed is shown under a break rather than left out.
        if (view.Me is { } me && me.Rank > view.Top.Length)
        {
            table.AddRow("[grey]...[/]", "[grey]...[/]", "[grey]...[/]");
            AddRow(table, me, own: true);
        }

        AnsiConsole.Write(table);

        if (view.Me is null)
        {
            Ui.Warn(CommonStrings.RankUnranked);
        }
    }

    private static void AddRow(Table table, LeaderboardEntry entry, bool own) => table.AddRow(
        new Markup(Highlight($"{entry.Rank}", own)),
        new Markup(Highlight(Markup.Escape(entry.DisplayName), own)),
        new Markup(Highlight($"{entry.Score}", own)));

    private static string Highlight(string text, bool own) => own ? $"[green]{text}[/]" : text;

    private static IReadOnlyList<Choice<LeaderboardKind?>> Boards() =>
    [
        new Choice<LeaderboardKind?>(CommonStrings.RankTitleRating, LeaderboardKind.Rating),
        new Choice<LeaderboardKind?>(CommonStrings.RankTitleDaily, LeaderboardKind.DailyWins),
        new Choice<LeaderboardKind?>(Strings.MenuBack, null),
    ];

    private static string Title(LeaderboardKind kind) =>
        kind == LeaderboardKind.DailyWins ? CommonStrings.RankTitleDaily : CommonStrings.RankTitleRating;

    private static string ScoreColumn(LeaderboardKind kind) =>
        kind == LeaderboardKind.DailyWins ? CommonStrings.RankColumnWins : CommonStrings.RankColumnRating;
}
