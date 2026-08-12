using Grpc.Core;
using MagicOnion.Server.Hubs;
using SharpCampus.GameCore;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Shared.Duel;

namespace SharpCampus.RoomServer.Hubs;

// A thin adapter over the room mailbox: nothing here touches the simulation, and only the two calls
// that owe the caller an answer wait for the loop to produce one.
public sealed class DuelHub(RoomManager rooms) : StreamingHubBase<IDuelHub, IDuelHubReceiver>, IDuelHub
{
    private DuelRoom? _room;
    private int _playerIndex = -1;

    public async Task<JoinRoomResult> JoinAsync(JoinRoomRequest request)
    {
        if (_room is not null)
        {
            return JoinRoomResult.Rejected;
        }

        var room = rooms.GetOrCreate(request.RoomKey);
        var group = await Group.AddAsync($"room:{request.RoomKey}");

        var completion = new TaskCompletionSource<JoinRoomResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = room.TryPost(RoomCommand.Join(request.DisplayName, group, completion))
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
        _room?.TryPost(RoomCommand.Forfeit(_playerIndex));
        return Task.CompletedTask;
    }

    protected override ValueTask OnDisconnected()
    {
        _room?.TryPost(RoomCommand.Disconnect(_playerIndex));
        return default;
    }
}
