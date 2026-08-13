using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Server.Common.Security;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Services;

namespace SharpCampus.ApiServer.Matchmaking;

[Authorize]
public sealed class MatchmakingService(
    IUserContext userContext,
    IMatchQueue queue,
    ITicketStore tickets,
    IActiveRoomStore activeRooms,
    EntryTokenService entryTokens)
    : ServiceBase<IMatchmakingService>, IMatchmakingService
{
    public async UnaryResult<MatchStatusResponse> EnqueueAsync()
    {
        var userId = userContext.UserId;

        // A client that lost its answer retries, and a client whose match was already made must get the
        // ticket rather than a place in the queue.
        if (await tickets.GetAsync(userId) is { } ticket)
        {
            return new MatchStatusResponse(MatchQueueState.Matched, ticket);
        }

        // A client that died mid-match goes back to the room it fell out of instead of queueing for a
        // new one, which makes rerunning the duel command the whole of the reconnect flow. The token it
        // was paired with expired long ago, so this signs a fresh one.
        if (await activeRooms.GetAsync(userId) is { } room)
        {
            return new MatchStatusResponse(
                MatchQueueState.Matched,
                new MatchTicket(room.RoomId, room.Endpoint, entryTokens.Issue(userId, room.RoomId)));
        }

        await queue.EnqueueAsync(userId);
        return MatchStatusResponse.Waiting;
    }

    public async UnaryResult CancelAsync() => await queue.RemoveAsync(userContext.UserId);

    public async UnaryResult<MatchStatusResponse> GetStatusAsync()
    {
        var userId = userContext.UserId;

        if (await tickets.GetAsync(userId) is { } ticket)
        {
            return new MatchStatusResponse(MatchQueueState.Matched, ticket);
        }

        return await queue.ContainsAsync(userId) ? MatchStatusResponse.Waiting : MatchStatusResponse.Idle;
    }
}
