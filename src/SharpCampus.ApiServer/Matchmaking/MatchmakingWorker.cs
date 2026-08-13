using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Server.Common.Security;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Internal.Rooms;
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
    EntryTokenService entryTokens,
    IServiceScopeFactory scopes,
    ILogger<MatchmakingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan _period = TimeSpan.FromMilliseconds(500);

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
        }
        finally
        {
            await pairingLock.ReleaseAsync();
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
        var players = new[]
        {
            new RoomPlayer(first, await NicknameAsync(first)),
            new RoomPlayer(second, await NicknameAsync(second)),
        };

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

    // The worker is a singleton and the profile store is scoped, so each lookup gets its own scope.
    private async Task<string> NicknameAsync(UserId userId)
    {
        using var scope = scopes.CreateScope();
        var profiles = scope.ServiceProvider.GetRequiredService<IProfileRepository>();

        return (await profiles.GetAsync(userId))?.Nickname ?? NicknameRules.CreateInitial(userId);
    }
}
