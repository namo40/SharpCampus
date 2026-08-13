using Grpc.Core;
using MagicOnion.Server.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using SharpCampus.GameCore;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Security;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;

namespace SharpCampus.RoomServer.Hubs;

// A thin adapter over the room mailbox: nothing here touches the simulation, and only the two calls
// that owe the caller an answer wait for the loop to produce one.
[Authorize]
public sealed class DuelHub(RoomManager rooms, EntryTokenService entryTokens)
    : StreamingHubBase<IDuelHub, IDuelHubReceiver>, IDuelHub
{
    private DuelRoom? _room;
    private int _playerIndex = -1;

    public async Task<JoinRoomResult> JoinAsync(JoinRoomRequest request)
    {
        // Entry is decided entirely from the token, the connection's identity and the room's seating
        // plan, none of which the loop thread owns, so the mailbox only ever sees calls that passed.
        if (_room is not null
            || !entryTokens.TryValidate(request.EntryToken, out var tokenUserId, out var tokenRoomId)
            || tokenRoomId != request.RoomId
            || tokenUserId != CallerUserId()
            || !rooms.TryGet(request.RoomId, out var room)
            || room is null
            || !room.IsExpected(tokenUserId))
        {
            return JoinRoomResult.Rejected;
        }

        var group = await Group.AddAsync($"room:{request.RoomId}");

        var completion = new TaskCompletionSource<JoinRoomResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = room.TryPost(RoomCommand.Join(tokenUserId, Context.ContextId, group, completion))
            ? await completion.Task
            : JoinRoomResult.Rejected;

        if (result is not { Accepted: true, PlayerIndex: { } seat })
        {
            await group.RemoveAsync(Context);
            return result;
        }

        _room = room;
        _playerIndex = seat.AsPrimitive();
        return result;
    }

    public Task SendInputsAsync(GameInput[] inputs)
    {
        if (_room is not null && inputs.Length > 0)
        {
            _room.TryPost(RoomCommand.SendInputs(_playerIndex, inputs));
        }

        return Task.CompletedTask;
    }

    public async Task<DuelSnapshot> RequestSnapshotAsync()
    {
        if (_room is null)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Join a room before reading it."));
        }

        var completion = new TaskCompletionSource<DuelSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_room.TryPost(RoomCommand.Snapshot(completion)))
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "The room is no longer running."));
        }

        return await completion.Task;
    }

    public Task ForfeitAsync()
    {
        _room?.TryPost(RoomCommand.Forfeit(_playerIndex, Context.ContextId));
        return Task.CompletedTask;
    }

    // The connection id travels with it: a seat that has already been taken back by a newer connection
    // must not be emptied by the old one finally noticing it is gone.
    protected override ValueTask OnDisconnected()
    {
        _room?.TryPost(RoomCommand.Disconnect(_playerIndex, Context.ContextId));
        return default;
    }

    // A hub call has no request scope of its own, so IUserContext cannot serve it: the principal comes
    // off the HTTP/2 connection the hub was established on, which [Authorize] has already vetted.
    private UserId CallerUserId() =>
        UserId.Parse(Context.CallContext.GetHttpContext().User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
}
