using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using MagicOnion.Server.Hubs;
using MessagePipe;
using SharpCampus.GameCore;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Values;

namespace SharpCampus.RoomServer.Rooms;

internal enum RoomState : byte
{
    WaitingForPlayers,
    Countdown,
    Playing,
    Finished,
    Closed,
}

// One room is one LogicLooper action. Every field below is written on the loop thread only: hub
// threads reach the room through the command mailbox and nowhere else.
internal sealed class DuelRoom
{
    private readonly ConcurrentQueue<RoomCommand> _commands = new();
    private readonly object _mailboxGate = new();
    private readonly RoomPlayer[] _players;
    private readonly DuelRules _rules;
    private readonly ILogger _logger;
    private readonly IAsyncPublisher<MatchFinishedEvent> _matchFinished;
    private readonly Action<DuelRoom>? _onClosed;
    private readonly Seat[] _seats = [new(), new()];
    private readonly List<GameInput>[] _queuedInputs = [[], []];
    private readonly GameInput[][] _tickInputs;

    private DuelSimulation _simulation;
    private IGroup<IDuelHubReceiver>? _group;
    private RoomState _closingFrom;
    private int _stateTicks;
    private volatile bool _closed;

    public DuelRoom(
        RoomId roomId,
        RoomPlayer[] players,
        DuelRules rules,
        ILogger logger,
        IAsyncPublisher<MatchFinishedEvent> matchFinished,
        Action<DuelRoom>? onClosed = null)
    {
        RoomId = roomId;
        _players = players;
        _rules = rules;
        _logger = logger;
        _matchFinished = matchFinished;
        _onClosed = onClosed;
        _tickInputs = [new GameInput[rules.InputPerTickMax], new GameInput[rules.InputPerTickMax]];

        var seed = NewSeed();
        _simulation = new DuelSimulation(rules.Simulation, seed);
        logger.RoomCreated(roomId, seed);
    }

    public RoomId RoomId { get; }

    // Identifies the game rather than the room: a rematch is settled on its own.
    public MatchId MatchId { get; private set; } = MatchId.New();

    public RoomState State { get; private set; } = RoomState.WaitingForPlayers;

    // ReSharper disable once InconsistentlySynchronizedField
    public bool IsClosed => _closed;

    public MatchResult? Result { get; private set; }

    // The seating plan is fixed when the room is created and never mutated, so both the hub and the
    // manager may read it off their own threads.
    public RoomPlayer[] Players => _players;

    public bool IsExpected(UserId userId) => SeatOf(userId) >= 0;

    // Rejecting once the room is closing is what keeps a hub from awaiting a reply nobody will send.
    public bool TryPost(RoomCommand command)
    {
        lock (_mailboxGate)
        {
            if (_closed)
            {
                return false;
            }

            _commands.Enqueue(command);
            return true;
        }
    }

    // The loop body, kept separate from the looper registration so tests can drive it a tick at a time.
    // Returns false once the room is done and the action should be unregistered.
    internal bool Tick()
    {
        DrainCommands();

        switch (State)
        {
            case RoomState.WaitingForPlayers:
                TickWaitingForPlayers();
                break;
            case RoomState.Countdown:
                if (TickGrace())
                {
                    TickCountdown();
                }

                break;
            case RoomState.Playing:
                if (TickGrace())
                {
                    TickPlaying();
                }

                break;
            case RoomState.Finished:
                TickRematchOffer();
                break;
        }

        if (State != RoomState.Closed)
        {
            return true;
        }

        Close();
        return false;
    }

    // Seeds are per game and server-side: both boards deal from one, so a client that knew it could
    // read the opponent's piece order.
    private static ulong NewSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private int SeatOf(UserId userId)
    {
        for (var seat = 0; seat < _players.Length; seat++)
        {
            if (_players[seat].UserId == userId)
            {
                return seat;
            }
        }

        return -1;
    }

    private void DrainCommands()
    {
        while (_commands.TryDequeue(out var command))
        {
            switch (command.Kind)
            {
                case RoomCommandKind.Join:
                    HandleJoin(command);
                    break;
                case RoomCommandKind.Inputs:
                    _queuedInputs[command.PlayerIndex].AddRange(command.Inputs!);
                    break;
                case RoomCommandKind.Forfeit:
                    HandleLeave(command, MatchEndReason.Forfeit);
                    break;
                case RoomCommandKind.Disconnect:
                    HandleLeave(command, MatchEndReason.Disconnect);
                    break;
                case RoomCommandKind.Snapshot:
                    command.SnapshotCompletion!.TrySetResult(CreateSnapshot());
                    break;
                case RoomCommandKind.RematchAnswer:
                    // First answer wins: a seat that dropped out of the offer has already declined.
                    _seats[command.PlayerIndex].RematchAnswer ??= command.Accepted;
                    break;
            }
        }
    }

    private void HandleJoin(in RoomCommand command)
    {
        var playerIndex = SeatOf(command.UserId);
        if (playerIndex < 0)
        {
            command.JoinCompletion!.TrySetResult(JoinRoomResult.Rejected);
            return;
        }

        var seat = _seats[playerIndex];

        // A match in progress takes its player back, and only that player: a seat whose connection is
        // still alive is not free, and an offer that is being answered is past the point of rejoining.
        if (State is RoomState.Countdown or RoomState.Playing)
        {
            if (seat.Connected)
            {
                command.JoinCompletion!.TrySetResult(JoinRoomResult.Rejected);
                return;
            }

            Resume(playerIndex, command);
            return;
        }

        if (State != RoomState.WaitingForPlayers || seat.Taken)
        {
            command.JoinCompletion!.TrySetResult(JoinRoomResult.Rejected);
            return;
        }

        seat.Taken = true;
        seat.Connected = true;
        seat.ConnectionId = command.ConnectionId;

        // Both hubs resolve the same group instance from the same name, so the first one to arrive
        // gives the room everything it needs to broadcast.
        _group ??= command.Group;
        _logger.RoomSeated(RoomId, playerIndex, _players[playerIndex].DisplayName);

        var waiting = !_seats[1 - playerIndex].Taken;
        command.JoinCompletion!.TrySetResult(new JoinRoomResult(true, new PlayerIndex(playerIndex), waiting));

        if (!waiting)
        {
            StartCountdown();
        }
    }

    private void Resume(int playerIndex, in RoomCommand command)
    {
        var seat = _seats[playerIndex];
        seat.Connected = true;
        seat.ConnectionId = command.ConnectionId;
        seat.GraceTicksLeft = 0;
        _group ??= command.Group;

        command.JoinCompletion!.TrySetResult(
            new JoinRoomResult(true, new PlayerIndex(playerIndex), WaitingForOpponent: false));

        Everyone()?.OnPresenceChanged(new PlayerIndex(playerIndex), true);

        // Announcing the match again puts the returning client back on its usual start-then-snapshot
        // path, which is what rebuilds the whole board on its screen.
        _group?.Single(seat.ConnectionId).OnMatchStarting(StartInfo());
        _logger.RoomSeatResumed(RoomId, playerIndex);
    }

    private void HandleLeave(in RoomCommand command, MatchEndReason reason)
    {
        var playerIndex = command.PlayerIndex;
        if (playerIndex < 0 || _seats[playerIndex].ConnectionId != command.ConnectionId)
        {
            // A connection the seat has already replaced is a zombie: whatever it says arrives too late.
            return;
        }

        switch (State)
        {
            case RoomState.Countdown or RoomState.Playing when reason == MatchEndReason.Disconnect:
                Drop(playerIndex);
                break;
            case RoomState.Countdown or RoomState.Playing:
                // Giving up is deliberate, so it ends the match on the spot. The grace period is for
                // connections that failed, not for players who chose to stop.
                Finish(playerIndex == 0 ? DuelOutcome.Player2Wins : DuelOutcome.Player1Wins, reason);
                break;
            case RoomState.Finished:
                _seats[playerIndex].Connected = false;
                _seats[playerIndex].RematchAnswer ??= false;
                break;
            case RoomState.WaitingForPlayers:
                _seats[playerIndex].Taken = false;
                _seats[playerIndex].Connected = false;
                if (!_seats[0].Taken && !_seats[1].Taken)
                {
                    Stop();
                }

                break;
        }
    }

    private void Drop(int playerIndex)
    {
        var seat = _seats[playerIndex];
        seat.Connected = false;
        seat.GraceTicksLeft = _rules.GraceTicks;

        Everyone()?.OnPresenceChanged(new PlayerIndex(playerIndex), false);
        _logger.RoomSeatLost(RoomId, playerIndex, _rules.GraceTicks);
    }

    // Runs before the game does: the match carries on without the dropped player's inputs until their
    // grace runs out. Returns false once that has ended the match.
    private bool TickGrace()
    {
        var first = Expired(0);
        var second = Expired(1);

        if (first && second)
        {
            Finish(DuelOutcome.Draw, MatchEndReason.Disconnect);
            return false;
        }

        if (first || second)
        {
            Finish(first ? DuelOutcome.Player2Wins : DuelOutcome.Player1Wins, MatchEndReason.Disconnect);
            return false;
        }

        return true;

        bool Expired(int playerIndex)
        {
            var seat = _seats[playerIndex];
            return !seat.Connected && seat.GraceTicksLeft > 0 && --seat.GraceTicksLeft == 0;
        }
    }

    private void TickWaitingForPlayers()
    {
        _stateTicks++;
        if (_stateTicks < _rules.JoinTimeoutTicks)
        {
            return;
        }

        // Whoever did sit down is waiting on a match that will never start, and the only channel they
        // are listening on is the one the match result comes down.
        if (_seats[0].Taken || _seats[1].Taken)
        {
            Result = new MatchResult(DuelOutcome.Draw, null, MatchEndReason.Aborted);
            Everyone()?.OnMatchFinished(Result);
        }

        Stop();
    }

    private void TickCountdown()
    {
        _stateTicks++;
        if (_stateTicks >= _rules.CountdownTicks)
        {
            _stateTicks = 0;

            // Anything sent before the board was live is not part of the match, so the first playing
            // tick must not consume a countdown's worth of queued moves.
            _queuedInputs[0].Clear();
            _queuedInputs[1].Clear();
            State = RoomState.Playing;
        }
    }

    private void TickPlaying()
    {
        var result = _simulation.Tick(TakeInputs(0), TakeInputs(1));

        // The group proxy only serialises onto each connection's write queue, so the loop never waits
        // on a client here.
        Everyone()?.OnTickDelta(TickDeltaFactory.Create(result, _simulation.Board1, _simulation.Board2));

        if (result.Finished)
        {
            Finish(result.Outcome, MatchEndReason.TopOut);
        }
    }

    private void TickRematchOffer()
    {
        _stateTicks++;

        if (_seats[0].RematchAnswer == true && _seats[1].RematchAnswer == true)
        {
            StartRematch();
            return;
        }

        // One refusal settles it for both: an accepting player should not have to sit out the rest of
        // a timeout that can no longer produce a match.
        if (_seats[0].RematchAnswer == false
            || _seats[1].RematchAnswer == false
            || _stateTicks >= _rules.RematchTimeoutTicks)
        {
            CancelRematchOffer();
            Everyone()?.OnRematchDeclined();
            _logger.RoomRematchDeclined(RoomId);
            Stop();
        }
    }

    private ReadOnlySpan<GameInput> TakeInputs(int playerIndex)
    {
        var queued = _queuedInputs[playerIndex];
        var buffer = _tickInputs[playerIndex];
        var count = Math.Min(queued.Count, buffer.Length);
        for (var i = 0; i < count; i++)
        {
            buffer[i] = queued[i];
        }

        // Anything past the allowance is dropped rather than carried over: a flood must not buy a
        // player extra actions on later ticks.
        queued.Clear();
        return buffer.AsSpan(0, count);
    }

    private void StartCountdown()
    {
        State = RoomState.Countdown;
        _stateTicks = 0;

        Everyone()?.OnMatchStarting(StartInfo());
        _logger.RoomStarting(RoomId, _rules.CountdownTicks);
    }

    private MatchStartInfo StartInfo() => new(
        [
            new MatchPlayerInfo(new PlayerIndex(0), _players[0].DisplayName),
            new MatchPlayerInfo(new PlayerIndex(1), _players[1].DisplayName),
        ],
        _rules.CountdownTicks,
        _rules.NextCount);

    private void Finish(DuelOutcome outcome, MatchEndReason reason)
    {
        Result = new MatchResult(outcome, MatchResult.WinnerOf(outcome), reason);
        Everyone()?.OnMatchFinished(Result);
        _logger.RoomFinished(RoomId, _simulation.TickNumber, outcome, reason);

        // Fire and forget: settlement talks to a database, and nothing a room does may make the loop
        // thread wait. A room that never started never gets here, and has no match to settle.
        _matchFinished.Publish(new MatchFinishedEvent(
            MatchId,
            RoomId,
            outcome,
            reason,
            _simulation.TickNumber,
            new MatchParticipant(_players[0].UserId, _simulation.Board1.Stats),
            new MatchParticipant(_players[1].UserId, _simulation.Board2.Stats)));

        // Only a match both players saw through to the end is worth replaying, and only while both of
        // them are still there to answer.
        if (reason is MatchEndReason.TopOut or MatchEndReason.Forfeit
            && _seats[0].Connected
            && _seats[1].Connected
            && _group is not null)
        {
            State = RoomState.Finished;
            _stateTicks = 0;

            try
            {
                OfferRematch(_group);
                return;
            }
            catch (ObjectDisposedException)
            {
                // Both players can be gone before the ask goes out: the group dies with its last
                // connection while this tick is still inside the finish, and the seats only learn of
                // the disconnects from commands a later tick would have drained.
            }
        }

        Stop();
    }

    // The question is asked from the loop thread but answered off it: a loop action cannot await, so
    // each ask runs as its own task and posts what it hears back into the mailbox.
    private void OfferRematch(IGroup<IDuelHubReceiver> group)
    {
        var timeoutSeconds = _rules.RematchTimeoutTicks / _rules.TickRate;

        for (var playerIndex = 0; playerIndex < _seats.Length; playerIndex++)
        {
            // Client results time out after five seconds by default, which is shorter than the offer,
            // so every call carries a token of its own.
            var ask = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 1));
            _seats[playerIndex].RematchAsk = ask;

            _ = AskRematchAsync(
                group.Single(_seats[playerIndex].ConnectionId),
                playerIndex,
                timeoutSeconds,
                ask.Token);
        }
    }

    private async Task AskRematchAsync(
        IDuelHubReceiver client,
        int playerIndex,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var accepted = false;

        try
        {
            accepted = await client.AskRematchAsync(timeoutSeconds, cancellationToken);
        }
        catch (Exception e)
        {
            // A client that errored, dropped or ran out the clock has all said the same thing.
            _logger.RoomRematchUnanswered(RoomId, playerIndex, e.Message);
        }

        TryPost(RoomCommand.RematchAnswer(playerIndex, accepted));
    }

    private void StartRematch()
    {
        CancelRematchOffer();

        foreach (var seat in _seats)
        {
            seat.RematchAnswer = null;
        }

        _queuedInputs[0].Clear();
        _queuedInputs[1].Clear();
        Result = null;

        var seed = NewSeed();
        _simulation = new DuelSimulation(_rules.Simulation, seed);
        MatchId = MatchId.New();
        _logger.RoomRematching(RoomId, seed);

        StartCountdown();
    }

    private void CancelRematchOffer()
    {
        foreach (var seat in _seats)
        {
            seat.RematchAsk?.Cancel();
            seat.RematchAsk?.Dispose();
            seat.RematchAsk = null;
        }
    }

    // Multicaster disposes a group with the last connection that leaves it, and a room often has news
    // for players who are already gone: a result nobody stayed for, a decline nobody is owed. A dead
    // group is the same as no group at all.
    private IDuelHubReceiver? Everyone()
    {
        try
        {
            return _group?.All;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    // Runs off the loop thread, and only once the loop action has died with an exception: nothing else
    // can move this room to Closed any more, so whoever observed the fault ends the match here.
    internal void Abort()
    {
        if (Result is null)
        {
            Result = new MatchResult(DuelOutcome.Draw, null, MatchEndReason.Aborted);
            Everyone()?.OnMatchFinished(Result);
        }

        Stop();
        Close();
    }

    // The one way out of every state: what the room was doing is kept for the log.
    private void Stop()
    {
        _closingFrom = State;
        State = RoomState.Closed;
    }

    private void Close()
    {
        lock (_mailboxGate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
        }

        CancelRematchOffer();

        // Whatever slipped into the mailbox before the gate shut still gets an answer.
        while (_commands.TryDequeue(out var command))
        {
            command.JoinCompletion?.TrySetResult(JoinRoomResult.Rejected);
            command.SnapshotCompletion?.TrySetResult(CreateSnapshot());
        }

        _onClosed?.Invoke(this);
        _logger.RoomClosed(RoomId, _closingFrom);
    }

    private DuelSnapshot CreateSnapshot()
        => TickDeltaFactory.CreateSnapshot(_simulation.TickNumber, _simulation.Board1, _simulation.Board2);

    // A seat outlives the connection sitting in it: a dropped player keeps theirs until the grace
    // period runs out, and the room needs the connection id to ask that one client anything.
    private sealed class Seat
    {
        public bool Taken { get; set; }

        public bool Connected { get; set; }

        public Guid ConnectionId { get; set; }

        public int GraceTicksLeft { get; set; }

        public bool? RematchAnswer { get; set; }

        public CancellationTokenSource? RematchAsk { get; set; }
    }
}
