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
}

// The single mailbox every hub call goes through. Hub threads only ever enqueue one of these; the
// loop thread is what turns them into state changes.
internal readonly record struct RoomCommand(
    RoomCommandKind Kind,
    int PlayerIndex,
    UserId UserId,
    IGroup<IDuelHubReceiver>? Group,
    GameInput[]? Inputs,
    TaskCompletionSource<JoinRoomResult>? JoinCompletion,
    TaskCompletionSource<DuelSnapshot>? SnapshotCompletion)
{
    public static RoomCommand Join(
        UserId userId,
        IGroup<IDuelHubReceiver> group,
        TaskCompletionSource<JoinRoomResult> completion)
        => new(RoomCommandKind.Join, -1, userId, group, null, completion, null);

    public static RoomCommand SendInputs(int playerIndex, GameInput[] inputs)
        => new(RoomCommandKind.Inputs, playerIndex, default, null, inputs, null, null);

    public static RoomCommand Forfeit(int playerIndex)
        => new(RoomCommandKind.Forfeit, playerIndex, default, null, null, null, null);

    public static RoomCommand Disconnect(int playerIndex)
        => new(RoomCommandKind.Disconnect, playerIndex, default, null, null, null, null);

    public static RoomCommand Snapshot(TaskCompletionSource<DuelSnapshot> completion)
        => new(RoomCommandKind.Snapshot, -1, default, null, null, null, completion);
}
