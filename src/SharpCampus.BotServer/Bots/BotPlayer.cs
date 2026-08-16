using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.Extensions.Options;
using R3;
using SharpCampus.BotServer.Configuration;
using SharpCampus.GameCore;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Services;
using SharpCampus.Shared.Values;

namespace SharpCampus.BotServer.Bots;

// One bot, one match: the same queue calls, the same ticket, the same hub a console client uses. The
// only thing that differs is where the inputs come from.
internal sealed class BotPlayer(
    BotAccountPool pool,
    IOptions<BotServerOptions> options,
    TimeProvider time,
    ILogger<BotPlayer> logger) : IBotRunner
{
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds(60);

    private readonly BotServerOptions _options = options.Value;

    public async Task PlayAsync(BotAccount account, CancellationToken cancellationToken)
    {
        using var apiChannel = GrpcChannel.ForAddress(_options.ApiServerAddress);

        if (await WaitForTicketAsync(account, apiChannel, cancellationToken) is not { } ticket)
        {
            return;
        }

        logger.BotMatched(account.Nickname, ticket.RoomId, ticket.Endpoint);
        await PlayAsync(account, ticket, cancellationToken);
    }

    private async Task<MatchTicket?> WaitForTicketAsync(
        BotAccount account,
        GrpcChannel channel,
        CancellationToken cancellationToken)
    {
        var matchmaking = Matchmaking(account, channel);

        MatchStatusResponse status;
        try
        {
            status = await matchmaking.EnqueueAsync();
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            // The cached token has aged out. One fresh sign-in is the whole of the recovery.
            if (!await pool.RefreshAsync(account))
            {
                return null;
            }

            matchmaking = Matchmaking(account, channel);
            status = await matchmaking.EnqueueAsync();
        }

        logger.BotQueued(account.Nickname);

        var timeout = TimeSpan.FromSeconds(_options.QueueTimeoutSeconds);
        var deadline = time.GetUtcNow() + timeout;

        // Polling rather than being pushed to, because that is what the console client does: a bot
        // that queued any other way would not be exercising the same path.
        while (status.State == MatchQueueState.Queued)
        {
            if (time.GetUtcNow() >= deadline)
            {
                await matchmaking.CancelAsync();
                logger.BotQueueTimedOut(account.Nickname, (int)timeout.TotalSeconds);
                return null;
            }

            await Task.Delay(_pollInterval, time, cancellationToken);
            status = await matchmaking.GetStatusAsync();
        }

        return status.State == MatchQueueState.None ? null : status.Ticket;
    }

    private async Task PlayAsync(BotAccount account, MatchTicket ticket, CancellationToken cancellationToken)
    {
        using var roomChannel = GrpcChannel.ForAddress(ticket.Endpoint);
        using var receiver = new BotReceiver();

        // No client heartbeat: the room server runs one in the other direction, which is what notices
        // a bot that vanishes, and a bot has no round trip to show anybody.
        var hubOptions = StreamingHubClientOptions.CreateWithDefault(
            callOptions: new CallOptions(Authorization(account)));

        var hub = await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
            roomChannel,
            receiver,
            hubOptions,
            cancellationToken: cancellationToken);

        try
        {
            await PlayAsync(account, hub, receiver, ticket, cancellationToken);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    private async Task PlayAsync(
        BotAccount account,
        IDuelHub hub,
        BotReceiver receiver,
        MatchTicket ticket,
        CancellationToken cancellationToken)
    {
        var join = await hub.JoinAsync(new JoinRoomRequest(ticket.RoomId, ticket.EntryToken));
        if (join is not { Accepted: true, PlayerIndex: { } seat })
        {
            logger.BotSeatRejected(account.Nickname, ticket.RoomId);
            return;
        }

        logger.BotSeated(account.Nickname, seat.AsPrimitive(), ticket.RoomId);

        // The room gives up on an opponent that never arrives by itself, so this wait can end with a
        // start or with the aborted match's result.
        Task outcome;
        try
        {
            outcome = await Task.WhenAny(receiver.Starting, receiver.Finished)
                .WaitAsync(_startTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return;
        }

        if (outcome == receiver.Starting)
        {
            await PlayGameAsync(hub, receiver, seat, cancellationToken);
        }

        if (receiver.Finished.IsCompleted)
        {
            var result = await receiver.Finished;
            logger.BotMatchFinished(
                account.Nickname,
                receiver.Replica.Tick.AsPrimitive(),
                result.Outcome,
                result.Reason);
        }
    }

    private async Task PlayGameAsync(
        IDuelHub hub,
        BotReceiver receiver,
        PlayerIndex seat,
        CancellationToken cancellationToken)
    {
        receiver.ApplySnapshot(await hub.RequestSnapshotAsync());

        var cadence = TimeSpan.FromMilliseconds(_options.InputCadenceMilliseconds);

        // The whole of the bot's play. A spawn on its own board asks for a plan, the plan is the burst
        // of inputs that reaches the chosen drop, and the burst is spread over the room's ticks rather
        // than sent at once: one input per beat, in the order it was planned. Every burst ends in the
        // hard drop that causes the next spawn, so two plans can never be in flight together. Planning
        // runs on the hub's receive loop, which it can afford: a few dozen collision-free drops.
        using var play = Resumed(receiver, seat)
            .Concat(Spawns(receiver, seat))
            .Select(_ => receiver.Read(
                seat,
                static (index, replica) => BotPlanner.Plan(replica.Boards[index.AsPrimitive()])))
            .Where(static plan => plan.Length > 0)
            .SelectMany(plan => Paced(plan, cadence))
            .SubscribeAwait(hub, static (input, room, _) => new ValueTask(room.SendInputsAsync([input])));

        // A room that dies without a result would otherwise leave this task, and the account slot it
        // holds, waiting for a message that is never coming.
        await Task.WhenAny(receiver.Finished, hub.WaitForDisconnect()).WaitAsync(cancellationToken);
    }

    // A snapshot taken mid piece has nothing to wait for: the spawn that would have asked for a plan
    // happened before this bot was seated.
    private static Observable<Unit> Resumed(BotReceiver receiver, PlayerIndex seat)
        => receiver.Read(seat, static (index, replica) => replica.Boards[index.AsPrimitive()].ActivePiece is not null)
            ? Observable.Return(Unit.Default)
            : Observable.Empty<Unit>();

    private static Observable<Unit> Spawns(BotReceiver receiver, PlayerIndex seat)
        => receiver.Deltas
            .Where(seat, static (delta, index) => Spawned(delta, index))
            .Select(static _ => Unit.Default);

    private static bool Spawned(TickDelta delta, PlayerIndex seat)
    {
        foreach (var duelEvent in delta.Events)
        {
            if (duelEvent.Kind == TickEventKind.PieceSpawned && duelEvent.PlayerIndex == seat)
            {
                return true;
            }
        }

        return false;
    }

    // The cadence belongs to the stream rather than to a sleep in the middle of it, so disposing the
    // subscription stops the timer with it.
    private Observable<GameInput> Paced(GameInput[] plan, TimeSpan cadence)
        => Observable.Zip(plan.ToObservable(), Observable.Interval(cadence, time), static (input, _) => input);

    private static IMatchmakingService Matchmaking(BotAccount account, GrpcChannel channel)
        => MagicOnionClient.Create<IMatchmakingService>(channel).WithHeaders(Authorization(account));

    private static Metadata Authorization(BotAccount account)
        => new() { { "authorization", $"Bearer {account.AccessToken}" } };
}
