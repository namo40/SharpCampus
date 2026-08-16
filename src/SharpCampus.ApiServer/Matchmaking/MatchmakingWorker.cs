using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Server.Common.Security;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Internal.Bots;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Values;

namespace SharpCampus.ApiServer.Matchmaking;

// Pairing is the one piece of matchmaking that is not a client call: a single instance reads the shared
// queue, asks a room server for a room and leaves a ticket for each player to find.
internal sealed class MatchmakingWorker(
    IPairingLock pairingLock,
    IMatchQueue queue,
    ITicketStore tickets,
    IActiveRoomStore activeRooms,
    IRoomRegistry registry,
    IRoomControlClient roomControl,
    IBotSummoner bots,
    IBotSummonCooldown botCooldown,
    EntryTokenService entryTokens,
    IServiceScopeFactory scopes,
    MemoryDatabase masterData,
    IOptions<BotFallbackOptions> botFallback,
    ILogger<MatchmakingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan _period = TimeSpan.FromMilliseconds(500);

    // What a profile row is created wearing, and so what an account without one is seated in.
    private readonly SkinId _freeSkinId = masterData.SkinTable.FreeSkin.SkinId;

    private readonly TimeSpan _summonAfter = TimeSpan.FromSeconds(botFallback.Value.SummonAfterSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_period);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PairAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    internal async Task PairAsync()
    {
        if (!await pairingLock.TryAcquireAsync())
        {
            return;
        }

        try
        {
            // Holding the lock is what makes reading the pair and removing it two separate steps: no
            // other instance can take those two accounts in between. A pass stops at the first pair it
            // cannot place, because retrying it in the same pass would only spin on it.
            while (await queue.PeekPairAsync() is { } pair && await MatchAsync(pair.First, pair.Second))
            {
            }

            await SummonBotAsync();
        }
        finally
        {
            await pairingLock.ReleaseAsync();
        }
    }

    // Whoever is left over once every pair has been placed has nobody to be matched with. Past the
    // threshold the bot server is asked for one, and the bot queues like any other client: this pass
    // does nothing else for it, and the next one pairs it through the path above.
    private async Task SummonBotAsync()
    {
        if (await queue.PeekLoneAsync() is not { } lone
            || lone.Waited < _summonAfter
            || await botCooldown.IsActiveAsync())
        {
            return;
        }

        var waitedSeconds = (int)lone.Waited.TotalSeconds;
        var outcome = (await bots.SummonAsync())?.Outcome;

        // Every attempt paces the next one, whatever came of it. A bot server that is missing or out of
        // accounts is a normal state here, and the cooldown is what keeps it to one attempt per window
        // instead of one per pass, without a failure state of its own to recover from.
        await botCooldown.StartAsync();

        switch (outcome)
        {
            case SummonBotOutcome.Deployed:
                logger.BotSummoned(lone.UserId, waitedSeconds);
                break;

            case SummonBotOutcome.Exhausted:
                logger.BotExhausted(lone.UserId, waitedSeconds);
                break;

            default:
                logger.BotSummonFailed(lone.UserId, waitedSeconds);
                break;
        }
    }

    // Production note: this is plain arrival order. A live service would pair inside a rating band and
    // widen it the longer someone waits.
    private async Task<bool> MatchAsync(UserId first, UserId second)
    {
        var server = await registry.FindLeastLoadedAsync();
        if (server is null)
        {
            logger.NoRoomServer(first, second);
            return false;
        }

        var roomId = new RoomId(Ulid.NewUlid());
        var players = new[] { await SeatAsync(first), await SeatAsync(second) };

        var result = await roomControl.CreateRoomAsync(server.ControlEndpoint, new CreateRoomRequest(roomId, players));
        if (result is not { Outcome: CreateRoomOutcome.Created })
        {
            // The pair stays in the queue untouched, so the next pass picks the same two up again.
            logger.RoomRefused(roomId, server.Name, result?.Outcome.ToString() ?? "Unreachable", first, second);
            return false;
        }

        // Tickets before the queue, never the other way around: a client polling in between still finds
        // itself queued, where the reverse order would answer "not queued, no ticket" and look like a
        // dropped match.
        foreach (var player in players)
        {
            // The room entry outlives the ticket on purpose: it is what sends a client that died
            // mid-match back to the same room, long after the ticket that got it there expired.
            await activeRooms.StoreAsync(player.UserId, new ActiveRoom(roomId, server.ClientEndpoint));
            await tickets.StoreAsync(
                player.UserId,
                new MatchTicket(roomId, server.ClientEndpoint, entryTokens.Issue(player.UserId, roomId)));
        }

        // A player who cancelled while this was in flight is already out of the list, so their removal
        // matches nothing and they keep the ticket they were just issued.
        await queue.RemoveAsync(first);
        await queue.RemoveAsync(second);

        logger.MatchMade(first, second, roomId, server.Name);
        return true;
    }

    // Everything the room needs about an account comes from its profile, and an account that has never
    // called the ApiServer has none yet. The worker is a singleton and the profile store is scoped, so
    // each lookup gets its own scope.
    private async Task<RoomPlayer> SeatAsync(UserId userId)
    {
        using var scope = scopes.CreateScope();
        var profiles = scope.ServiceProvider.GetRequiredService<IProfileRepository>();
        var profile = await profiles.GetAsync(userId);

        return new RoomPlayer(
            userId,
            profile?.Nickname ?? NicknameRules.CreateInitial(userId),
            profile?.EquippedSkinId ?? _freeSkinId);
    }
}
