using System.Diagnostics;
using ConsoleAppFramework;
using Cysharp.Text;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Cli.Duel;
using SharpCampus.Cli.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Services;
using SharpCampus.Shared.Values;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class DuelCommand
{
    private const string ApiServerAddress = "http://localhost:5001";
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _offerWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _promptPollPeriod = TimeSpan.FromMilliseconds(50);

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
        // A player the server still has in a room is answered with that room rather than a queue place,
        // so rerunning this command is the whole of the reconnect flow.
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
        var status = new DuelStatus();
        var receiver = new DuelReceiver(status);

        // The client heartbeat is what measures the round trip the HUD shows. The server runs one of
        // its own in the other direction, which is what notices a client that vanishes without saying so.
        var options = StreamingHubClientOptions.CreateWithDefault(callOptions: new CallOptions(authorization))
            .WithClientHeartbeatInterval(_heartbeatInterval)
            .WithClientHeartbeatResponseReceived(heartbeat => status.Measure(heartbeat.RoundTripTime));

        var hub = await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
            roomChannel,
            receiver,
            options,
            cancellationToken: cancellationToken);

        try
        {
            await PlayAsync(hub, receiver, status, ticket, auto, cancellationToken);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    private static async Task PlayAsync(
        IDuelHub hub,
        DuelReceiver receiver,
        DuelStatus status,
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

        MatchResult result;

        // A room can run more than one game: an accepted rematch announces itself exactly the way the
        // first game did, so the whole flow repeats.
        do
        {
            var starting = receiver.Starting;
            var finished = receiver.Finished;

            // The room gives up on a missing opponent by itself, so this wait can end with a start or
            // with the aborted match's result.
            Task outcome;
            try
            {
                outcome = await Task.WhenAny(starting, finished).WaitAsync(_startTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.OpponentNeverArrived}[/]");
                return;
            }

            if (outcome == starting)
            {
                await RunGameAsync(hub, receiver, status, await starting, seat, auto, cancellationToken);
            }

            result = await finished.WaitAsync(cancellationToken);
            Report(result, receiver, seat);
        }
        while (await RematchAsync(receiver, result.Reason, cancellationToken));
    }

    private static async Task RunGameAsync(
        IDuelHub hub,
        DuelReceiver receiver,
        DuelStatus status,
        MatchStartInfo start,
        PlayerIndex seat,
        bool auto,
        CancellationToken cancellationToken)
    {
        receiver.ApplySnapshot(await hub.RequestSnapshotAsync());

        // A first snapshot that is already past tick zero means this client walked back into a match
        // the room never stopped running.
        if (receiver.Replica.Tick.AsPrimitive() > 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[green]{Strings.MatchResumed}[/]");
        }

        var renderer = new DuelRenderer(seat.AsPrimitive(), DisplayNames(start), Skins(start), status);
        await new DuelSession(hub, receiver, renderer, auto).RunAsync(cancellationToken);
    }

    // The room only offers a rematch when both players were still there at the end, so a match that
    // ended any other way is simply over.
    private static async Task<bool> RematchAsync(
        DuelReceiver receiver,
        MatchEndReason reason,
        CancellationToken cancellationToken)
    {
        if (reason is not (MatchEndReason.TopOut or MatchEndReason.Forfeit))
        {
            return false;
        }

        RematchOffer offer;
        try
        {
            offer = await receiver.Offered.WaitAsync(_offerWait, cancellationToken);
        }
        catch (TimeoutException)
        {
            return false;
        }

        switch (await ReadAnswerAsync(offer.TimeoutSeconds, cancellationToken))
        {
            case false:
                offer.Decline();
                AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.RematchDeclined}[/]");
                return false;

            case null:
                // Saying nothing is an answer of its own: the room's timeout is what ends the offer,
                // and staying connected until the room says so is what lets it. Only the decline can
                // be waited on here: without an accept, Starting is still the finished game's.
                AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.RematchNoAnswer}[/]");
                await WaitForDeclineAsync(receiver, offer, cancellationToken);
                return false;
        }

        offer.Accept();
        AnsiConsole.MarkupLineInterpolated($"[grey]{Strings.RematchWaiting}[/]");

        return await SettleAsync(receiver, offer, cancellationToken);
    }

    private static async Task WaitForDeclineAsync(
        DuelReceiver receiver,
        RematchOffer offer,
        CancellationToken cancellationToken)
    {
        try
        {
            await receiver.Declined.WaitAsync(TimeSpan.FromSeconds(offer.TimeoutSeconds + 2), cancellationToken);
        }
        catch (TimeoutException)
        {
        }

        AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.RematchDeclined}[/]");
    }

    private static async Task<bool> SettleAsync(
        DuelReceiver receiver,
        RematchOffer offer,
        CancellationToken cancellationToken)
    {
        var starting = receiver.Starting;
        var declined = receiver.Declined;
        var window = TimeSpan.FromSeconds(offer.TimeoutSeconds + 2);

        try
        {
            if (await Task.WhenAny(starting, declined).WaitAsync(window, cancellationToken) == starting)
            {
                return true;
            }
        }
        catch (TimeoutException)
        {
            // The room should have said one way or the other by now; treat the silence as a refusal.
        }

        AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.RematchDeclined}[/]");
        return false;
    }

    // An interactive console answers with a key press, a redirected stdin with a line, and an end of
    // input not at all. That last case is deliberate: it is what leaves the offer to the room's timeout.
    private static async Task<bool?> ReadAnswerAsync(int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (!Console.IsInputRedirected)
        {
            return await PromptAsync(timeoutSeconds, cancellationToken);
        }

        try
        {
            var line = await Task.Run(Console.In.ReadLine, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);

            return line is null ? null : line.Trim() is ['y' or 'Y', ..];
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private static async Task<bool?> PromptAsync(int timeoutSeconds, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            while (timeoutSeconds - (int)Stopwatch.GetElapsedTime(started).TotalSeconds is var remaining and > 0)
            {
                Console.Out.Write(ZString.Concat("\r", Localization.Format(Strings.RematchPrompt, remaining), "   "));

                if (TakeAnswer() is { } answer)
                {
                    return answer;
                }

                await Task.Delay(_promptPollPeriod, cancellationToken);
            }

            return null;
        }
        finally
        {
            Console.Out.Write("\r\n");
        }
    }

    private static bool? TakeAnswer()
    {
        while (Console.KeyAvailable)
        {
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.Y:
                    return true;
                case ConsoleKey.N:
                    return false;
            }
        }

        return null;
    }

    private static void Report(MatchResult result, DuelReceiver receiver, PlayerIndex seat)
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

    // The room sends each seat's skin along with its name, which is the only place the client gets one.
    private static Skin[] Skins(MatchStartInfo start)
    {
        var skins = new Skin[2];
        foreach (var player in start.Players)
        {
            skins[player.PlayerIndex.AsPrimitive()] = player.Skin;
        }

        return skins;
    }
}
