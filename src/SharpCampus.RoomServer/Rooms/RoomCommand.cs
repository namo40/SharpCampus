using MagicOnion.Server.Hubs;
using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;

namespace SharpCampus.RoomServer.Rooms;

internal enum RoomCommandKind : byte
{
    Join,
    Inputs,
    Forfeit,
    Disconnect,
    Snapshot,
    RematchAnswer,
}

// The single mailbox every hub call goes through. Hub threads only ever enqueue one of these; the
// loop thread is what turns them into state changes.
internal readonly record struct RoomCommand(
    RoomCommandKind Kind,
    int PlayerIndex,
    UserId UserId,
    Guid ConnectionId,
    bool Accepted,
    IGroup<IDuelHubReceiver>? Group,
    GameInput[]? Inputs,
    TaskCompletionSource<JoinRoomResult>? JoinCompletion,
    TaskCompletionSource<DuelSnapshot>? SnapshotCompletion)
{
    public static RoomCommand Join(
        UserId userId,
        Guid connectionId,
        IGroup<IDuelHubReceiver> group,
        TaskCompletionSource<JoinRoomResult> completion)
        => new(RoomCommandKind.Join, -1, userId, connectionId, false, group, null, completion, null);

    public static RoomCommand SendInputs(int playerIndex, GameInput[] inputs)
        => new(RoomCommandKind.Inputs, playerIndex, default, default, false, null, inputs, null, null);

    public static RoomCommand Forfeit(int playerIndex, Guid connectionId = default)
        => new(RoomCommandKind.Forfeit, playerIndex, default, connectionId, false, null, null, null, null);

    public static RoomCommand Disconnect(int playerIndex, Guid connectionId = default)
        => new(RoomCommandKind.Disconnect, playerIndex, default, connectionId, false, null, null, null, null);

    public static RoomCommand Snapshot(TaskCompletionSource<DuelSnapshot> completion)
        => new(RoomCommandKind.Snapshot, -1, default, default, false, null, null, null, completion);

    // Posted by the task that asked a client, not by a hub call: the answer has to reach the room on
    // the loop thread like every other change.
    public static RoomCommand RematchAnswer(int playerIndex, bool accepted)
        => new(RoomCommandKind.RematchAnswer, playerIndex, default, default, accepted, null, null, null, null);
}
