using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpCampus.GameCore;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public class DuelRoomTests
{
    private const ulong Seed = 0x5EEDUL;

    private static readonly RoomId _roomId = new(Ulid.NewUlid());

    [Fact]
    public void EachAccountTakesTheSeatItWasGiven()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = CreateRoom(RoomFixture.Rules(countdownTicks: 3));

        // The second account joins first, and still lands in the seat the room reserved for it.
        var second = room.Join(group, RoomFixture.SecondUser);
        var first = room.Join(group, RoomFixture.FirstUser);

        Assert.Equal(new JoinRoomResult(true, new PlayerIndex(1), WaitingForOpponent: true), second);
        Assert.Equal(new JoinRoomResult(true, new PlayerIndex(0), WaitingForOpponent: false), first);
        Assert.Equal(RoomState.Countdown, room.State);

        var starting = Assert.Single(receiver.Started);
        Assert.Equal(["one", "two"], starting.Players.Select(x => x.DisplayName));
        Assert.Equal(3, starting.CountdownTicks);
        Assert.Empty(receiver.Deltas);

        room.TickToPlaying();
        room.Tick();

        Assert.Single(receiver.Deltas);
    }

    [Fact]
    public void AccountTheRoomIsNotWaitingFor_IsTurnedAway()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = CreateRoom(RoomFixture.Rules());

        Assert.Equal(JoinRoomResult.Rejected, room.Join(group, new UserId(Guid.NewGuid())));
        Assert.False(room.IsExpected(new UserId(Guid.NewGuid())));
    }

    [Fact]
    public void SecondJoinFromTheSameAccount_IsTurnedAway()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = CreateRoom(RoomFixture.Rules());

        room.Join(group, RoomFixture.FirstUser);

        Assert.Equal(JoinRoomResult.Rejected, room.Join(group, RoomFixture.FirstUser));
    }

    [Fact]
    public void Playing_BroadcastsADeltaEveryTickEvenWithoutEvents()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(receiver, RoomFixture.Rules()).TickToPlaying();

        for (var i = 0; i < 5; i++)
        {
            room.Tick();
        }

        Assert.Equal(5, receiver.Deltas.Count);
        Assert.Equal([1, 2, 3, 4, 5], receiver.Deltas.Select(x => x.Tick.AsPrimitive()));
    }

    [Fact]
    public void Inputs_BeyondTheTickAllowanceAreDiscardedRatherThanDeferred()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(receiver, RoomFixture.Rules(inputPerTickMax: 2)).TickToPlaying();

        // Gravity is frozen in the test rules, so the only thing that can lock a piece is a hard drop.
        room.TryPost(RoomCommand.SendInputs(0, [
            GameInput.HardDrop,
            GameInput.HardDrop,
            GameInput.HardDrop,
            GameInput.HardDrop,
            GameInput.HardDrop,
        ]));

        for (var i = 0; i < 6; i++)
        {
            room.Tick();
        }

        Assert.Equal(1, LockCount(receiver));
    }

    [Fact]
    public void InputsSentDuringTheCountdown_DoNotSurviveIntoTheMatch()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(receiver, RoomFixture.Rules(countdownTicks: 3));

        Assert.Equal(RoomState.Countdown, room.State);
        room.TryPost(RoomCommand.SendInputs(0, [GameInput.HardDrop, GameInput.HardDrop]));

        room.TickToPlaying();
        room.Tick();

        Assert.Equal(0, LockCount(receiver));
    }

    [Fact]
    public void Forfeit_HandsTheMatchToTheOpponentAndClosesTheRoom()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(receiver, RoomFixture.Rules()).TickToPlaying();

        room.TryPost(RoomCommand.Forfeit(0));

        Assert.False(room.Tick());
        Assert.Equal(RoomState.Closed, room.State);
        Assert.True(room.IsClosed);

        var result = Assert.Single(receiver.Finished);
        Assert.Equal(DuelOutcome.Player2Wins, result.Outcome);
        Assert.Equal<PlayerIndex?>(new PlayerIndex(1), result.WinnerPlayerIndex);
        Assert.Equal(MatchEndReason.Forfeit, result.Reason);
    }

    [Fact]
    public void Disconnect_DuringAMatchIsAnImmediateLoss()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(receiver, RoomFixture.Rules()).TickToPlaying();

        room.TryPost(RoomCommand.Disconnect(1));

        Assert.False(room.Tick());

        var result = Assert.Single(receiver.Finished);
        Assert.Equal(DuelOutcome.Player1Wins, result.Outcome);
        Assert.Equal(MatchEndReason.Disconnect, result.Reason);
    }

    [Fact]
    public void JoinTimeout_TellsTheWaitingPlayerTheMatchWasAbandoned()
    {
        var receiver = new RecordingReceiver();
        var room = CreateRoom(RoomFixture.Rules(joinTimeoutTicks: 3));
        room.Join(RoomFixture.Group(receiver), RoomFixture.FirstUser);

        Assert.True(room.Tick());
        Assert.False(room.Tick());
        Assert.Equal(RoomState.Closed, room.State);
        Assert.Empty(receiver.Started);

        var result = Assert.Single(receiver.Finished);
        Assert.Equal(DuelOutcome.Draw, result.Outcome);
        Assert.Null(result.WinnerPlayerIndex);
        Assert.Equal(MatchEndReason.Aborted, result.Reason);
    }

    [Fact]
    public void JoinTimeout_WithNobodySeatedTellsNobody()
    {
        var receiver = new RecordingReceiver();
        var room = CreateRoom(RoomFixture.Rules(joinTimeoutTicks: 2));

        Assert.True(room.Tick());
        Assert.False(room.Tick());
        Assert.Empty(receiver.Finished);
    }

    [Fact]
    public async Task ClosedRoom_RejectsNewCommandsAndAnswersTheOnesInFlight()
    {
        var room = SeatedRoom(new RecordingReceiver(), RoomFixture.Rules()).TickToPlaying();

        var completion = new TaskCompletionSource<JoinRoomResult>();
        room.TryPost(RoomCommand.Forfeit(0));
        room.TryPost(RoomCommand.Join(RoomFixture.FirstUser, RoomFixture.Group(new RecordingReceiver()), completion));
        room.Tick();

        Assert.Equal(JoinRoomResult.Rejected, await completion.Task);
        Assert.False(room.TryPost(RoomCommand.Forfeit(1)));
    }

    [Fact]
    public async Task Snapshot_IsAnsweredFromTheLoopThread()
    {
        var room = SeatedRoom(new RecordingReceiver(), RoomFixture.Rules()).TickToPlaying();
        room.Tick();

        var completion = new TaskCompletionSource<DuelSnapshot>();
        room.TryPost(RoomCommand.Snapshot(completion));
        room.Tick();

        var snapshot = await completion.Task;
        Assert.Equal(2, snapshot.Players.Length);
        Assert.Equal(DuelReplicaBoard.Width * DuelReplicaBoard.Height, snapshot.Players[0].Cells.Length);
        Assert.Equal(5, snapshot.Players[0].Next.Length);
    }

    private static int LockCount(RecordingReceiver receiver) => receiver.Deltas
        .SelectMany(delta => delta.Events)
        .Count(duelEvent => duelEvent.Kind == TickEventKind.PieceLocked);

    [Fact]
    public void FinishAfterBothPlayersVanished_StillClosesTheRoom()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = CreateRoom(RoomFixture.Rules());
        room.Join(group, RoomFixture.FirstUser);
        room.Join(group, RoomFixture.SecondUser);
        room.TickToPlaying();

        // Both connections die and take the group with them before the room hears about either seat.
        group.All.Returns(_ => throw new ObjectDisposedException("group"));
        room.TryPost(RoomCommand.Forfeit(0));

        Assert.False(room.Tick());
        Assert.True(room.IsClosed);
    }

    [Fact]
    public void AbortedRoom_EndsInAnAbortedDrawForWhoeverStillListens()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(receiver, RoomFixture.Rules()).TickToPlaying();
        room.Tick();

        room.Abort();

        Assert.True(room.IsClosed);
        var result = Assert.Single(receiver.Finished);
        Assert.Equal(DuelOutcome.Draw, result.Outcome);
        Assert.Equal(MatchEndReason.Aborted, result.Reason);
    }

    private static DuelRoom CreateRoom(DuelRules rules)
        => new(_roomId, RoomFixture.Players(), rules, Seed, NullLogger.Instance);

    private static DuelRoom SeatedRoom(RecordingReceiver receiver, DuelRules rules)
    {
        var group = RoomFixture.Group(receiver);
        var room = CreateRoom(rules);
        room.Join(group, RoomFixture.FirstUser);
        room.Join(group, RoomFixture.SecondUser);
        return room;
    }
}
