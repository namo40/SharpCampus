using System.Diagnostics;
using Cysharp.Text;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Client.Common;
using SharpCampus.Client.Common.Duel;
using SharpCampus.Client.Resources;
using SharpCampus.Client.Screens;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Values;
using CommonStrings = SharpCampus.Client.Common.Resources.Strings;

namespace SharpCampus.Client.Duel;

// Queue, room, match and rematch, from the lobby and back to it. The duel owns the console outright
// once the first frame is drawn, so everything here happens either side of that.
internal sealed class DuelFlow(ApiGateway gateway) : Screen(gateway)
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _offerWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _keyPollPeriod = TimeSpan.FromMilliseconds(50);

    protected override async Task<ScreenResult> ShowAsync()
    {
        Ui.Begin(Strings.DuelTitle);

        if (await WaitForTicketAsync() is { } ticket)
        {
            await PlayAsync(ticket);
        }

        Ui.Pause();

        return ScreenResult.Back;
    }

    // A duel reaches past the ApiServer to the room it was given, so which server was not there is
    // whatever the failed call said rather than the one address this client knows by heart.
    protected override string Unreachable(RpcException exception) =>
        Localization.Format(CommonStrings.ServerUnreachableDetail, exception.Status.Detail);

    private async Task<MatchTicket?> WaitForTicketAsync()
    {
        // A player the server still has in a room is answered with that room rather than a queue
        // place, so opening this screen again is the whole of the reconnect flow.
        var status = await Gateway.Matchmaking.EnqueueAsync();
        Ui.Info(CommonStrings.Queued);
        Ui.Hint(Strings.QueueCancelHint);

        // Production note: a live service pushes the match to the client. Polling keeps the contract
        // to three plain Unary calls and needs nothing from the identity provider.
        while (status.State == MatchQueueState.Queued)
        {
            if (await CancelledAsync())
            {
                await Gateway.Matchmaking.CancelAsync();
                Ui.Warn(CommonStrings.QueueLeft);
                return null;
            }

            status = await Gateway.Matchmaking.GetStatusAsync();
        }

        if (status.State == MatchQueueState.None)
        {
            Ui.Warn(CommonStrings.QueueReleased);
            return null;
        }

        return status.Ticket;
    }

    // The wait between polls is also where the keyboard is read, so leaving the queue answers as
    // quickly as pressing the key rather than at the end of the second.
    private static async Task<bool> CancelledAsync()
    {
        var started = Stopwatch.GetTimestamp();

        while (Stopwatch.GetElapsedTime(started) < _pollInterval)
        {
            while (Console.KeyAvailable)
            {
                if (Console.ReadKey(intercept: true).Key == ConsoleKey.Escape)
                {
                    return true;
                }
            }

            await Task.Delay(_keyPollPeriod);
        }

        return false;
    }

    private async Task PlayAsync(MatchTicket ticket)
    {
        Ui.Info(Localization.Format(CommonStrings.Matched, ticket.RoomId, ticket.Endpoint));

        using var roomChannel = GrpcChannel.ForAddress(ticket.Endpoint);
        var status = new DuelStatus();
        var receiver = new DuelReceiver(status);

        // The entry point routes the stream by this header to the server that owns the room; a
        // connection straight to a room server ignores it.
        var hubHeaders = new Metadata { { "room-id", ticket.RoomId.ToString() } };
        foreach (var header in Gateway.Authorization)
        {
            hubHeaders.Add(header);
        }

        // The client heartbeat is what measures the round trip the HUD shows. The server runs one of
        // its own in the other direction, which is what notices a client that vanishes without saying so.
        var options = StreamingHubClientOptions.CreateWithDefault(callOptions: new CallOptions(hubHeaders))
            .WithClientHeartbeatInterval(_heartbeatInterval)
            .WithClientHeartbeatResponseReceived(heartbeat => status.Measure(heartbeat.RoundTripTime));

        var hub = await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(roomChannel, receiver, options);

        try
        {
            await PlayAsync(hub, receiver, status, ticket);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    private static async Task PlayAsync(IDuelHub hub, DuelReceiver receiver, DuelStatus status, MatchTicket ticket)
    {
        var join = await hub.JoinAsync(new JoinRoomRequest(ticket.RoomId, ticket.EntryToken));
        if (join is not { Accepted: true, PlayerIndex: { } seat })
        {
            Ui.Error(Localization.Format(CommonStrings.SeatRejected, ticket.RoomId));
            return;
        }

        Ui.Info(Localization.Format(CommonStrings.Seated, seat.AsPrimitive()));
        if (join.WaitingForOpponent)
        {
            Ui.Hint(CommonStrings.WaitingForOpponent);
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
                outcome = await Task.WhenAny(starting, finished).WaitAsync(_startTimeout);
            }
            catch (TimeoutException)
            {
                Ui.Warn(CommonStrings.OpponentNeverArrived);
                return;
            }

            if (outcome == starting)
            {
                await RunGameAsync(hub, receiver, status, await starting, seat);
            }

            result = await finished;
            Report(result, receiver, seat);
        }
        while (await RematchAsync(receiver, result.Reason));
    }

    private static async Task RunGameAsync(
        IDuelHub hub,
        DuelReceiver receiver,
        DuelStatus status,
        MatchStartInfo start,
        PlayerIndex seat)
    {
        receiver.ApplySnapshot(await hub.RequestSnapshotAsync());

        // A first snapshot that is already past tick zero means this client walked back into a match
        // the room never stopped running.
        if (receiver.Replica.Tick.AsPrimitive() > 0)
        {
            Ui.Info(CommonStrings.MatchResumed);
        }

        var renderer = new DuelRenderer(seat.AsPrimitive(), DisplayNames(start), Skins(start), status);
        await new DuelSession(hub, receiver, renderer, autoPlay: false).RunAsync(CancellationToken.None);
    }

    // The room only offers a rematch when both players were still there at the end, so a match that
    // ended any other way is simply over.
    private static async Task<bool> RematchAsync(DuelReceiver receiver, MatchEndReason reason)
    {
        if (reason is not (MatchEndReason.TopOut or MatchEndReason.Forfeit))
        {
            return false;
        }

        RematchOffer offer;
        try
        {
            offer = await receiver.Offered.WaitAsync(_offerWait);
        }
        catch (TimeoutException)
        {
            return false;
        }

        switch (await ReadAnswerAsync(offer.TimeoutSeconds))
        {
            case false:
                offer.Decline();
                Ui.Warn(CommonStrings.RematchDeclined);
                return false;

            case null:
                // Saying nothing is an answer of its own: the room's timeout is what ends the offer,
                // and staying connected until the room says so is what lets it. Only the decline can
                // be waited on here: without an accept, Starting is still the finished game's.
                Ui.Warn(CommonStrings.RematchNoAnswer);
                await WaitForDeclineAsync(receiver, offer);
                return false;
        }

        offer.Accept();
        Ui.Hint(CommonStrings.RematchWaiting);

        return await SettleAsync(receiver, offer);
    }

    private static async Task WaitForDeclineAsync(DuelReceiver receiver, RematchOffer offer)
    {
        try
        {
            await receiver.Declined.WaitAsync(TimeSpan.FromSeconds(offer.TimeoutSeconds + 2));
        }
        catch (TimeoutException)
        {
        }

        Ui.Warn(CommonStrings.RematchDeclined);
    }

    private static async Task<bool> SettleAsync(DuelReceiver receiver, RematchOffer offer)
    {
        var starting = receiver.Starting;
        var declined = receiver.Declined;
        var window = TimeSpan.FromSeconds(offer.TimeoutSeconds + 2);

        try
        {
            if (await Task.WhenAny(starting, declined).WaitAsync(window) == starting)
            {
                return true;
            }
        }
        catch (TimeoutException)
        {
            // The room should have said one way or the other by now; treat the silence as a refusal.
        }

        Ui.Warn(CommonStrings.RematchDeclined);
        return false;
    }

    // Answering nothing at all is a real case: it is what leaves the offer to the room's own timeout.
    private static async Task<bool?> ReadAnswerAsync(int timeoutSeconds)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            while (timeoutSeconds - (int)Stopwatch.GetElapsedTime(started).TotalSeconds is var remaining and > 0)
            {
                Console.Out.Write(ZString.Concat("\r", Localization.Format(CommonStrings.RematchPrompt, remaining), "   "));

                if (TakeAnswer() is { } answer)
                {
                    return answer;
                }

                await Task.Delay(_keyPollPeriod);
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
            MatchEndReason.Aborted => CommonStrings.ResultAbandoned,
            _ => result.WinnerPlayerIndex switch
            {
                null => CommonStrings.ResultDraw,
                { } winner when winner == seat => CommonStrings.ResultWin,
                _ => CommonStrings.ResultLose,
            },
        };

        var reason = result.Reason switch
        {
            MatchEndReason.TopOut => CommonStrings.ReasonTopOut,
            MatchEndReason.Forfeit => CommonStrings.ReasonForfeit,
            MatchEndReason.Disconnect => CommonStrings.ReasonDisconnect,
            _ => CommonStrings.ReasonAborted,
        };

        Ui.Info($"{verdict} ({reason})");
        Ui.Hint(Localization.Format(
            CommonStrings.ReplicaStats,
            receiver.Replica.Tick.AsPrimitive(),
            receiver.Replica.HasMissedTicks));
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
