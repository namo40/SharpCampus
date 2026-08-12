using System.Collections.Concurrent;
using MagicOnion.Server.Hubs;
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
    private readonly Action<DuelRoom>? _onClosed;
    private readonly DuelSimulation _simulation;
    private readonly bool[] _seated = new bool[2];
    private readonly List<GameInput>[] _queuedInputs = [[], []];
    private readonly GameInput[][] _tickInputs;

    private IGroup<IDuelHubReceiver>? _group;
    private int _stateTicks;
    private volatile bool _closed;

    public DuelRoom(
        RoomId roomId,
        RoomPlayer[] players,
        DuelRules rules,
        ulong seed,
        ILogger logger,
        Action<DuelRoom>? onClosed = null)
    {
        RoomId = roomId;
        _players = players;
        _rules = rules;
        _logger = logger;
        _onClosed = onClosed;
        _simulation = new DuelSimulation(rules.Simulation, seed);
        _tickInputs = [new GameInput[rules.InputPerTickMax], new GameInput[rules.InputPerTickMax]];

        logger.RoomCreated(roomId, seed);
    }

    public RoomId RoomId { get; }

    public RoomState State { get; private set; } = RoomState.WaitingForPlayers;

    public bool IsClosed => _closed;

    public MatchResult? Result { get; private set; }

    // The seating plan is fixed when the room is created and never mutated, so the hub may read it
    // off its own thread to turn away anyone the room is not waiting for.
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
                TickCountdown();
                break;
            case RoomState.Playing:
                TickPlaying();
                break;
        }

        if (State is RoomState.Finished or RoomState.Closed)
        {
            Close();
            return false;
        }

        return true;
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
                    HandleLeave(command.PlayerIndex, MatchEndReason.Forfeit);
                    break;
                case RoomCommandKind.Disconnect:
                    HandleLeave(command.PlayerIndex, MatchEndReason.Disconnect);
                    break;
                case RoomCommandKind.Snapshot:
                    command.SnapshotCompletion!.TrySetResult(CreateSnapshot());
                    break;
            }
        }
    }

    private void HandleJoin(in RoomCommand command)
    {
        var playerIndex = SeatOf(command.UserId);
        if (State != RoomState.WaitingForPlayers || playerIndex < 0 || _seated[playerIndex])
        {
            command.JoinCompletion!.TrySetResult(JoinRoomResult.Rejected);
            return;
        }

        _seated[playerIndex] = true;

        // Both hubs resolve the same group instance from the same name, so the first one to arrive
        // gives the room everything it needs to broadcast.
        _group ??= command.Group;
        _logger.RoomSeated(RoomId, playerIndex, _players[playerIndex].DisplayName);

        var waiting = !_seated[1 - playerIndex];
        command.JoinCompletion!.TrySetResult(new JoinRoomResult(true, new PlayerIndex(playerIndex), waiting));

        if (!waiting)
        {
            StartCountdown();
        }
    }

    private void HandleLeave(int playerIndex, MatchEndReason reason)
    {
        if (State is RoomState.Countdown or RoomState.Playing)
        {
            // Production note: a live service holds the seat open for a grace period before ruling a
            // dropped connection a loss, and lets the player reconnect into the running match.
            Finish(playerIndex == 0 ? DuelOutcome.Player2Wins : DuelOutcome.Player1Wins, reason);
            return;
        }

        if (State != RoomState.WaitingForPlayers || playerIndex < 0)
        {
            return;
        }

        _seated[playerIndex] = false;
        if (!_seated[0] && !_seated[1])
        {
            State = RoomState.Closed;
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
        if (_seated[0] || _seated[1])
        {
            Result = new MatchResult(DuelOutcome.Draw, null, MatchEndReason.Aborted);
            Everyone()?.OnMatchFinished(Result);
        }

        State = RoomState.Closed;
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

        Everyone()?.OnMatchStarting(new MatchStartInfo(
            [
                new MatchPlayerInfo(new PlayerIndex(0), _players[0].DisplayName),
                new MatchPlayerInfo(new PlayerIndex(1), _players[1].DisplayName),
            ],
            _rules.CountdownTicks,
            _rules.NextCount));

        _logger.RoomStarting(RoomId, _rules.CountdownTicks);
    }

    private void Finish(DuelOutcome outcome, MatchEndReason reason)
    {
        State = RoomState.Finished;
        Result = new MatchResult(outcome, MatchResult.WinnerOf(outcome), reason);
        Everyone()?.OnMatchFinished(Result);
        _logger.RoomFinished(RoomId, _simulation.TickNumber, outcome, reason);
    }

    private void Close()
    {
        var from = State;
        State = RoomState.Closed;

        lock (_mailboxGate)
        {
            _closed = true;
        }

        // Whatever slipped into the mailbox before the gate shut still gets an answer.
        while (_commands.TryDequeue(out var command))
        {
            command.JoinCompletion?.TrySetResult(JoinRoomResult.Rejected);
            command.SnapshotCompletion?.TrySetResult(CreateSnapshot());
        }

        _onClosed?.Invoke(this);
        _logger.RoomClosed(RoomId, from);
    }

    private DuelSnapshot CreateSnapshot()
        => TickDeltaFactory.CreateSnapshot(_simulation.TickNumber, _simulation.Board1, _simulation.Board2);

    // Multicaster disposes a group with the last connection that leaves it, and a room can have news
    // for players who are already gone: both connections can drop inside one tick window. A dead group
    // is the same as no group at all.
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
    // can close this room any more, so whoever observed the fault ends the match for anyone still
    // listening.
    internal void Abort()
    {
        if (Result is null)
        {
            Result = new MatchResult(DuelOutcome.Draw, null, MatchEndReason.Aborted);
            Everyone()?.OnMatchFinished(Result);
        }

        Close();
    }
}
