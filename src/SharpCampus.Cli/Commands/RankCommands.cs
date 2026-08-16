using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Client.Common;
using SharpCampus.Client.Common.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Services;
using Spectre.Console;
using CliStrings = SharpCampus.Cli.Resources.Strings;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class RankCommands
{
    private const string ApiServerLabel = "ApiServer";
    private const string DailyBoard = "daily";

    /// <summary>Shows the leading hundred of a board and where you stand on it.</summary>
    /// <param name="board">Which board: nothing for the rating board, daily for today's wins.</param>
    [Command("rank")]
    public async Task ShowAsync([Argument] string board = "")
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        if (KindOf(board) is not { } kind)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{CliStrings.RankUsage}[/]");
            return;
        }

        using var channel = GrpcChannel.ForAddress(ClientEndpoints.ApiServer);
        var client = MagicOnionClient.Create<ILeaderboardService>(channel)
            .WithHeaders(new Metadata { { "authorization", $"Bearer {session.AccessToken}" } });

        try
        {
            Render(kind, await client.GetLeaderboardAsync(kind));
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

    // The board is named by a bare word, so `rank` and `rank daily` read the way the other list
    // commands do. Anything else is a typo rather than a third board.
    private static LeaderboardKind? KindOf(string board) => board.ToLowerInvariant() switch
    {
        "" => LeaderboardKind.Rating,
        DailyBoard => LeaderboardKind.DailyWins,
        _ => null,
    };

    private static void Render(LeaderboardKind kind, LeaderboardView view)
    {
        if (view.Top.Length == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.RankEmpty}[/]");
            return;
        }

        AnsiConsole.MarkupLineInterpolated($"[grey]{Title(kind)}[/]");

        var table = new Table();
        table.AddColumn(Strings.RankColumnRank);
        table.AddColumn(Strings.RankColumnPlayer);
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
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.RankUnranked}[/]");
        }
    }

    private static void AddRow(Table table, LeaderboardEntry entry, bool own) => table.AddRow(
        new Markup(Highlight($"{entry.Rank}", own)),
        new Markup(Highlight(Markup.Escape(entry.DisplayName), own)),
        new Markup(Highlight($"{entry.Score}", own)));

    private static string Highlight(string text, bool own) => own ? $"[green]{text}[/]" : text;

    private static string Title(LeaderboardKind kind) =>
        kind == LeaderboardKind.DailyWins ? Strings.RankTitleDaily : Strings.RankTitleRating;

    private static string ScoreColumn(LeaderboardKind kind) =>
        kind == LeaderboardKind.DailyWins ? Strings.RankColumnWins : Strings.RankColumnRating;
}
