using Microsoft.Extensions.Logging.Abstractions;
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
    private static readonly RoomId _roomId = new(Ulid.NewUlid());

    [Fact]
    public void EachAccountTakesTheSeatItWasGiven()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = CreateRoom(RoomFixture.Rules(countdownTicks: 3));

        // The second account joins first, and still lands in the seat the room reserved for it.
        var second = room.Join(group, RoomFixture.SecondUser, RoomFixture.Connections[1]);
        var first = room.Join(group, RoomFixture.FirstUser, RoomFixture.Connections[0]);

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

        room.Join(group, RoomFixture.FirstUser, RoomFixture.Connections[0]);

        Assert.Equal(JoinRoomResult.Rejected, room.Join(group, RoomFixture.FirstUser, Guid.NewGuid()));
    }

    [Fact]
    public void Playing_BroadcastsADeltaEveryTickEvenWithoutEvents()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(RoomFixture.Group(receiver), RoomFixture.Rules()).TickToPlaying();

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
        var room = SeatedRoom(RoomFixture.Group(receiver), RoomFixture.Rules(inputPerTickMax: 2)).TickToPlaying();

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
        var room = SeatedRoom(RoomFixture.Group(receiver), RoomFixture.Rules(countdownTicks: 3));

        Assert.Equal(RoomState.Countdown, room.State);
        room.TryPost(RoomCommand.SendInputs(0, [GameInput.HardDrop, GameInput.HardDrop]));

        room.TickToPlaying();
        room.Tick();

        Assert.Equal(0, LockCount(receiver));
    }

    [Fact]
    public void Forfeit_HandsTheMatchToTheOpponent()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = SeatedRoom(group, RoomFixture.Rules()).TickToPlaying();

        Forfeit(room, 0);
        Assert.True(room.Tick());

        var result = Assert.Single(receiver.Finished);
        Assert.Equal(DuelOutcome.Player2Wins, result.Outcome);
        Assert.Equal<PlayerIndex?>(new PlayerIndex(1), result.WinnerPlayerIndex);
        Assert.Equal(MatchEndReason.Forfeit, result.Reason);

        // Both players are still there, so the room stays up to ask them about another game.
        Assert.Equal(RoomState.Finished, room.State);
        Assert.False(room.IsClosed);
    }

    [Fact]
    public void JoinTimeout_TellsTheWaitingPlayerTheMatchWasAbandoned()
    {
        var receiver = new RecordingReceiver();
        var room = CreateRoom(RoomFixture.Rules(joinTimeoutTicks: 3));
        room.Join(RoomFixture.Group(receiver), RoomFixture.FirstUser, RoomFixture.Connections[0]);

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
    public void AbandonedMatch_IsNotWorthARematch()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = CreateRoom(RoomFixture.Rules(joinTimeoutTicks: 2));
        room.Join(group, RoomFixture.FirstUser, RoomFixture.Connections[0]);

        Assert.False(room.Tick());
        Assert.Empty(group.Connection(RoomFixture.Connections[0]).RematchAsks);
    }

    [Fact]
    public async Task ClosedRoom_RejectsNewCommandsAndAnswersTheOnesInFlight()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = SeatedRoom(group, RoomFixture.Rules()).TickToPlaying();
        Answers(group, first: false, second: false);

        var completion = new TaskCompletionSource<JoinRoomResult>();
        Forfeit(room, 0);
        room.TryPost(RoomCommand.Join(RoomFixture.FirstUser, Guid.NewGuid(), group, completion));

        Assert.False(room.Tick());
        Assert.Equal(JoinRoomResult.Rejected, await completion.Task);
        Assert.False(room.TryPost(RoomCommand.Forfeit(1, RoomFixture.Connections[1])));
    }

    [Fact]
    public async Task Snapshot_IsAnsweredFromTheLoopThread()
    {
        var room = SeatedRoom(RoomFixture.Group(new RecordingReceiver()), RoomFixture.Rules()).TickToPlaying();
        room.Tick();

        var completion = new TaskCompletionSource<DuelSnapshot>();
        room.TryPost(RoomCommand.Snapshot(completion));
        room.Tick();

        var snapshot = await completion.Task;
        Assert.Equal(2, snapshot.Players.Length);
        Assert.Equal(DuelReplicaBoard.Width * DuelReplicaBoard.Height, snapshot.Players[0].Cells.Length);
        Assert.Equal(5, snapshot.Players[0].Next.Length);
    }

    [Fact]
    public void ClosingRoom_HandsItselfToTheCleanupCallback()
    {
        DuelRoom? released = null;
        var room = new DuelRoom(
            _roomId,
            RoomFixture.Players(),
            RoomFixture.Rules(joinTimeoutTicks: 2),
            NullLogger.Instance,
            closing => released = closing);

        room.Tick();
        Assert.False(room.Tick());

        Assert.Same(room, released);
    }

    [Fact]
    public void Disconnect_HoldsTheSeatAndKeepsThePlayRunningUntilTheGraceExpires()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(RoomFixture.Group(receiver), RoomFixture.Rules(graceTicks: 4)).TickToPlaying();

        Disconnect(room, 1);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(room.Tick());
        }

        Assert.Equal(RoomState.Playing, room.State);
        Assert.Equal(3, receiver.Deltas.Count);
        Assert.Equal([(1, false)], receiver.Presence);
        Assert.Empty(receiver.Finished);

        Assert.False(room.Tick());

        var result = Assert.Single(receiver.Finished);
        Assert.Equal(DuelOutcome.Player1Wins, result.Outcome);
        Assert.Equal(MatchEndReason.Disconnect, result.Reason);
        Assert.Equal(RoomState.Closed, room.State);
    }

    [Fact]
    public void BothSidesDroppingTogether_EndsInADraw()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(RoomFixture.Group(receiver), RoomFixture.Rules(graceTicks: 2)).TickToPlaying();

        Disconnect(room, 0);
        Disconnect(room, 1);

        Assert.True(room.Tick());
        Assert.False(room.Tick());

        var result = Assert.Single(receiver.Finished);
        Assert.Equal(DuelOutcome.Draw, result.Outcome);
        Assert.Null(result.WinnerPlayerIndex);
        Assert.Equal(MatchEndReason.Disconnect, result.Reason);
    }

    [Fact]
    public void MatchEndedByADroppedConnection_IsNotWorthARematch()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = SeatedRoom(group, RoomFixture.Rules(graceTicks: 1)).TickToPlaying();

        Disconnect(room, 1);

        Assert.False(room.Tick());
        Assert.Empty(group.Connection(RoomFixture.Connections[0]).RematchAsks);
    }

    [Fact]
    public void DroppedPlayer_TakesTheSeatBackAndThePlayCarriesOn()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = SeatedRoom(group, RoomFixture.Rules(graceTicks: 10)).TickToPlaying();

        room.Tick();
        room.Tick();
        Disconnect(room, 1);
        room.Tick();

        var rejoined = Guid.NewGuid();
        var result = room.Join(group, RoomFixture.SecondUser, rejoined);

        Assert.Equal(new JoinRoomResult(true, new PlayerIndex(1), WaitingForOpponent: false), result);
        Assert.Equal([(1, false), (1, true)], receiver.Presence);

        // Only the returning connection is told the match is on, which is what sends it after a snapshot.
        Assert.Single(group.Connection(rejoined).Started);
        Assert.Empty(group.Connection(RoomFixture.Connections[1]).Started);

        // Nothing restarted: the boards carry on from the tick they were on.
        room.Tick();
        Assert.Equal(RoomState.Playing, room.State);
        Assert.Equal(5, receiver.Deltas[^1].Tick.AsPrimitive());

        // The grace the drop started is spent, so the seat is not lost later on.
        for (var i = 0; i < 20; i++)
        {
            Assert.True(room.Tick());
        }
    }

    [Fact]
    public void JoinIntoASeatWhoseConnectionIsAlive_IsTurnedAway()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = SeatedRoom(group, RoomFixture.Rules()).TickToPlaying();

        Assert.Equal(JoinRoomResult.Rejected, room.Join(group, RoomFixture.SecondUser, Guid.NewGuid()));
        Assert.Equal(RoomState.Playing, room.State);
    }

    [Fact]
    public void JoinWhileTheRematchOfferIsOpen_IsTurnedAway()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = SeatedRoom(group, RoomFixture.Rules()).TickToPlaying();

        Forfeit(room, 0);
        room.Tick();

        Assert.Equal(RoomState.Finished, room.State);
        Assert.Equal(JoinRoomResult.Rejected, room.Join(group, RoomFixture.SecondUser, Guid.NewGuid()));
    }

    [Fact]
    public void DisconnectFromAConnectionTheSeatHasReplaced_IsIgnored()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = SeatedRoom(group, RoomFixture.Rules(graceTicks: 10)).TickToPlaying();

        Disconnect(room, 1);
        room.Tick();
        room.Join(group, RoomFixture.SecondUser, Guid.NewGuid());

        // The connection that dropped notices it much later, by which time the seat is somebody else's.
        Disconnect(room, 1);

        Assert.True(room.Tick());
        Assert.Equal(RoomState.Playing, room.State);
        Assert.Equal([(1, false), (1, true)], receiver.Presence);
    }

    [Fact]
    public void RematchBothAccept_StartsANewGameInTheSameRoom()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = SeatedRoom(group, RoomFixture.Rules(rematchTimeoutTicks: 60)).TickToPlaying();
        Answers(group, first: true, second: true);

        room.Tick();
        Forfeit(room, 0);
        Assert.True(room.Tick());

        // Both seats are asked directly and told how long the offer stands. Both answers land while the
        // finishing tick is still draining, so the room goes straight into the next countdown.
        Assert.Equal([3], group.Connection(RoomFixture.Connections[0]).RematchAsks);
        Assert.Equal([3], group.Connection(RoomFixture.Connections[1]).RematchAsks);
        Assert.Equal(RoomState.Countdown, room.State);
        Assert.Null(room.Result);
        Assert.Equal(2, receiver.Started.Count);

        // The next game deals from its own seed and starts counting again from tick one.
        room.TickToPlaying();
        room.Tick();
        Assert.Equal(1, receiver.Deltas[^1].Tick.AsPrimitive());
        Assert.Equal(0, receiver.RematchDeclines);
    }

    [Fact]
    public void OneRefusal_TellsBothPlayersAndClosesTheRoom()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = SeatedRoom(group, RoomFixture.Rules(rematchTimeoutTicks: 60)).TickToPlaying();
        Answers(group, first: true, second: false);

        // Both answers reach the mailbox while the finishing tick is still draining it, so the offer
        // is settled without the room ever ticking again.
        Forfeit(room, 0);
        Assert.False(room.Tick());

        Assert.Equal(1, receiver.RematchDeclines);
        Assert.Equal(RoomState.Closed, room.State);
        Assert.Single(receiver.Started);
    }

    [Fact]
    public void OfferNobodyAnswers_ClosesTheRoomWhenItExpires()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = SeatedRoom(group, RoomFixture.Rules(rematchTimeoutTicks: 3)).TickToPlaying();

        Forfeit(room, 0);

        Assert.True(room.Tick());
        Assert.True(room.Tick());
        Assert.False(room.Tick());

        Assert.Equal(1, receiver.RematchDeclines);
        Assert.Equal(RoomState.Closed, room.State);
    }

    [Fact]
    public void DroppingOutOfTheOffer_CountsAsARefusal()
    {
        var receiver = new RecordingReceiver();
        var group = RoomFixture.Group(receiver);
        var room = SeatedRoom(group, RoomFixture.Rules(rematchTimeoutTicks: 60)).TickToPlaying();
        Answers(group, first: true, second: null);

        Forfeit(room, 0);
        Assert.True(room.Tick());

        Disconnect(room, 1);
        Assert.False(room.Tick());

        Assert.Equal(1, receiver.RematchDeclines);
        Assert.Equal(RoomState.Closed, room.State);
    }

    private static int LockCount(RecordingReceiver receiver) => receiver.Deltas
        .SelectMany(delta => delta.Events)
        .Count(duelEvent => duelEvent.Kind == TickEventKind.PieceLocked);

    [Fact]
    public void FinishAfterBothPlayersVanished_StillClosesTheRoom()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = SeatedRoom(group, RoomFixture.Rules()).TickToPlaying();

        // Both connections die and take the group with them before the room hears about either seat.
        group.Dispose();
        Forfeit(room, 0);

        Assert.False(room.Tick());
        Assert.Equal(RoomState.Closed, room.State);
    }

    [Fact]
    public void DeclineAfterTheGroupDied_StillClosesTheRoom()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = SeatedRoom(group, RoomFixture.Rules(rematchTimeoutTicks: 60)).TickToPlaying();

        Forfeit(room, 0);
        Assert.True(room.Tick());

        group.Dispose();
        Disconnect(room, 0);
        Disconnect(room, 1);

        Assert.False(room.Tick());
        Assert.Equal(RoomState.Closed, room.State);
    }

    [Fact]
    public void AbortedRoom_EndsInAnAbortedDrawForWhoeverStillListens()
    {
        var receiver = new RecordingReceiver();
        var room = SeatedRoom(RoomFixture.Group(receiver), RoomFixture.Rules()).TickToPlaying();
        room.Tick();

        room.Abort();

        Assert.True(room.IsClosed);
        var result = Assert.Single(receiver.Finished);
        Assert.Equal(DuelOutcome.Draw, result.Outcome);
        Assert.Equal(MatchEndReason.Aborted, result.Reason);
    }

    [Fact]
    public void AbortWithTheGroupAlreadyDead_StillCloses()
    {
        var group = RoomFixture.Group(new RecordingReceiver());
        var room = SeatedRoom(group, RoomFixture.Rules()).TickToPlaying();

        group.Dispose();
        room.Abort();

        Assert.True(room.IsClosed);
    }

    private static DuelRoom CreateRoom(DuelRules rules)
        => new(_roomId, RoomFixture.Players(), rules, NullLogger.Instance);

    private static DuelRoom SeatedRoom(FakeGroup group, DuelRules rules)
    {
        var room = CreateRoom(rules);
        room.Join(group, RoomFixture.FirstUser, RoomFixture.Connections[0]);
        room.Join(group, RoomFixture.SecondUser, RoomFixture.Connections[1]);
        return room;
    }

    private static void Forfeit(DuelRoom room, int playerIndex)
        => room.TryPost(RoomCommand.Forfeit(playerIndex, RoomFixture.Connections[playerIndex]));

    private static void Disconnect(DuelRoom room, int playerIndex)
        => room.TryPost(RoomCommand.Disconnect(playerIndex, RoomFixture.Connections[playerIndex]));

    // An answer left out is one the room never hears, which is what its own timeout is for.
    private static void Answers(FakeGroup group, bool? first, bool? second)
    {
        if (first is { } accepted)
        {
            group.Connection(RoomFixture.Connections[0]).RematchReply = Task.FromResult(accepted);
        }

        if (second is { } opponent)
        {
            group.Connection(RoomFixture.Connections[1]).RematchReply = Task.FromResult(opponent);
        }
    }
}
