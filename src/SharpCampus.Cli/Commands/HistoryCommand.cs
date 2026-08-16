using System.Globalization;
using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Client.Common;
using SharpCampus.Client.Common.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Services;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class HistoryCommand
{
    private const string ApiServerLabel = "ApiServer";

    // A fixed pattern rather than the machine's: the language is chosen by --lang while the date format
    // would still follow the OS, which reads as two different locales in one table.
    private const string PlayedAtFormat = "yyyy-MM-dd HH:mm";

    /// <summary>Lists the matches you have finished, newest first.</summary>
    [Command("history")]
    public async Task ShowAsync()
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        using var channel = GrpcChannel.ForAddress(ClientEndpoints.ApiServer);
        var client = MagicOnionClient.Create<IMatchHistoryService>(channel)
            .WithHeaders(new Metadata { { "authorization", $"Bearer {session.AccessToken}" } });

        try
        {
            Render(await client.GetMyMatchesAsync());
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

    private static void Render(MatchHistoryEntry[] entries)
    {
        if (entries.Length == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.HistoryEmpty}[/]");
            return;
        }

        AnsiConsole.MarkupLineInterpolated($"[grey]{Strings.HistoryHeader}[/]");

        var table = new Table();
        table.AddColumn(Strings.HistoryColumnDate);
        table.AddColumn(Strings.HistoryColumnOpponent);
        table.AddColumn(Strings.HistoryColumnResult);
        table.AddColumn(Strings.HistoryColumnReason);
        table.AddColumn(Strings.HistoryColumnRating);
        table.AddColumn(Strings.HistoryColumnCoins);

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
        ? $"[grey]{Markup.Escape(Strings.HistoryUnknownOpponent)}[/]"
        : Markup.Escape(entry.OpponentNickname);

    // The stored strings are the contract rather than an enum, so a value this build has never heard of
    // is shown as it was written down instead of being hidden.
    private static string Outcome(string outcome) => outcome switch
    {
        "win" => $"[green]{Strings.HistoryOutcomeWin}[/]",
        "loss" => $"[red]{Strings.HistoryOutcomeLoss}[/]",
        "draw" => $"[grey]{Strings.HistoryOutcomeDraw}[/]",
        _ => Markup.Escape(outcome),
    };

    private static string Reason(string reason) => reason switch
    {
        "top_out" => Strings.ReasonTopOut,
        "forfeit" => Strings.ReasonForfeit,
        "disconnect" => Strings.ReasonDisconnect,
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
