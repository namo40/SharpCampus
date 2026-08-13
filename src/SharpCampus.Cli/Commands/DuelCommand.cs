using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Cli.Duel;
using SharpCampus.Cli.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Services;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class DuelCommand
{
    private const string ApiServerAddress = "http://localhost:5001";
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Queues for a duel and plays the match out.</summary>
    /// <param name="auto">Plays the match with random inputs instead of the keyboard.</param>
    /// <param name="cancellationToken">Cancellation of the queue wait or the running match.</param>
    [Command("duel")]
    public async Task ExecuteAsync(bool auto = false, CancellationToken cancellationToken = default)
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        var authorization = new Metadata { { "authorization", $"Bearer {session.AccessToken}" } };

        using var apiChannel = GrpcChannel.ForAddress(ApiServerAddress);
        var matchmaking = MagicOnionClient.Create<IMatchmakingService>(apiChannel).WithHeaders(authorization);

        try
        {
            if (await WaitForTicketAsync(matchmaking, cancellationToken) is { } ticket)
            {
                await PlayAsync(ticket, authorization, auto, cancellationToken);
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Strings.SessionExpired}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachableDetail, e.Status.Detail)}[/]");
        }
    }

    private static async Task<MatchTicket?> WaitForTicketAsync(
        IMatchmakingService matchmaking,
        CancellationToken cancellationToken)
    {
        var status = await matchmaking.EnqueueAsync();
        AnsiConsole.MarkupLineInterpolated($"[green]{Strings.Queued}[/]");

        // Production note: a live service pushes the match to the client. Polling keeps the contract to
        // three plain Unary calls and needs nothing from the identity provider.
        while (status.State == MatchQueueState.Queued)
        {
            try
            {
                await Task.Delay(_pollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await matchmaking.CancelAsync();
                AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.QueueLeft}[/]");
                return null;
            }

            status = await matchmaking.GetStatusAsync();
        }

        if (status.State == MatchQueueState.None)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.QueueReleased}[/]");
            return null;
        }

        return status.Ticket;
    }

    private static async Task PlayAsync(
        MatchTicket ticket,
        Metadata authorization,
        bool auto,
        CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLineInterpolated($"[green]{Localization.Format(Strings.Matched, ticket.RoomId, ticket.Endpoint)}[/]");

        using var roomChannel = GrpcChannel.ForAddress(ticket.Endpoint);
        var receiver = new DuelReceiver();

        var hub = await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
            roomChannel,
            receiver,
            option: new CallOptions(authorization),
            cancellationToken: cancellationToken);

        try
        {
            await PlayAsync(hub, receiver, ticket, auto, cancellationToken);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    private static async Task PlayAsync(
        IDuelHub hub,
        DuelReceiver receiver,
        MatchTicket ticket,
        bool auto,
        CancellationToken cancellationToken)
    {
        var join = await hub.JoinAsync(new JoinRoomRequest(ticket.RoomId, ticket.EntryToken));
        if (join is not { Accepted: true, PlayerIndex: { } seat })
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.SeatRejected, ticket.RoomId)}[/]");
            return;
        }

        AnsiConsole.MarkupLineInterpolated($"[green]{Localization.Format(Strings.Seated, seat.AsPrimitive())}[/]");
        if (join.WaitingForOpponent)
        {
            AnsiConsole.MarkupLineInterpolated($"[grey]{Strings.WaitingForOpponent}[/]");
        }

        // The room gives up on a missing opponent by itself, so this wait can end with a start or
        // with the aborted match's result.
        Task outcome;
        try
        {
            outcome = await Task.WhenAny(receiver.Starting, receiver.Finished)
                .WaitAsync(_startTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.OpponentNeverArrived}[/]");
            return;
        }

        if (outcome == receiver.Starting)
        {
            receiver.ApplySnapshot(await hub.RequestSnapshotAsync());

            var renderer = new DuelRenderer(seat.AsPrimitive(), DisplayNames(await receiver.Starting));
            await new DuelSession(hub, receiver, renderer, auto).RunAsync(cancellationToken);
        }

        Report(await receiver.Finished.WaitAsync(cancellationToken), receiver, seat);
    }

    private static void Report(MatchResult result, DuelReceiver receiver, Shared.Values.PlayerIndex seat)
    {
        var verdict = result.Reason switch
        {
            MatchEndReason.Aborted => Strings.ResultAbandoned,
            _ => result.WinnerPlayerIndex switch
            {
                null => Strings.ResultDraw,
                { } winner when winner == seat => Strings.ResultWin,
                _ => Strings.ResultLose,
            },
        };

        var reason = result.Reason switch
        {
            MatchEndReason.TopOut => Strings.ReasonTopOut,
            MatchEndReason.Forfeit => Strings.ReasonForfeit,
            MatchEndReason.Disconnect => Strings.ReasonDisconnect,
            _ => Strings.ReasonAborted,
        };

        AnsiConsole.MarkupLineInterpolated($"[green]{verdict}[/] [grey]({reason})[/]");
        AnsiConsole.MarkupLineInterpolated(
            $"[grey]{Localization.Format(Strings.ReplicaStats, receiver.Replica.Tick.AsPrimitive(), receiver.Replica.HasMissedTicks)}[/]");
    }

    private static string[] DisplayNames(MatchStartInfo start)
    {
        var names = new string[2];
        foreach (var player in start.Players)
        {
            names[player.PlayerIndex.AsPrimitive()] = player.DisplayName;
        }

        return names;
    }
}
