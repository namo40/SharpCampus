using SharpCampus.GameCore.Internal;

namespace SharpCampus.GameCore;

// Two boards, garbage routing between them and the win condition. Both boards deal pieces from
// the same seed, so the players always face the identical piece sequence.
public sealed class DuelSimulation
{
    public const int MaxInputsPerTick = 10;

    private readonly BoardSimulation _board1;
    private readonly BoardSimulation _board2;
    private readonly List<TickEvent> _events = [];

    public DuelSimulation(SimulationConfig config, ulong seed)
        : this(config, new SevenBagPieceSource(seed), new SevenBagPieceSource(seed), seed)
    {
    }

    internal DuelSimulation(SimulationConfig config, IPieceSource pieces1, IPieceSource pieces2, ulong seed)
    {
        _board1 = new BoardSimulation(config, pieces1, seed, 0);
        _board2 = new BoardSimulation(config, pieces2, seed, 1);
        Board1 = _board1.View;
        Board2 = _board2.View;
    }

    public BoardView Board1 { get; }

    public BoardView Board2 { get; }

    public int TickNumber { get; private set; }

    public DuelOutcome Outcome { get; private set; } = DuelOutcome.Ongoing;

    public bool Finished => Outcome != DuelOutcome.Ongoing;

    public DuelTickResult Tick(ReadOnlySpan<GameInput> inputs1, ReadOnlySpan<GameInput> inputs2)
    {
        if (Finished)
        {
            return new DuelTickResult(TickNumber, [], true, Outcome);
        }

        TickNumber++;
        _events.Clear();

        var attack1 = _board1.Tick(Limit(inputs1), _events);
        var attack2 = _board2.Tick(Limit(inputs2), _events);

        // Attacks land only after both boards have advanced, so the order the boards are ticked in
        // can never favour one player.
        _board2.ReceiveGarbage(attack1);
        _board1.ReceiveGarbage(attack2);

        Judge();

        return new DuelTickResult(TickNumber, _events.Count == 0 ? [] : _events.ToArray(), Finished, Outcome);
    }

    internal BoardSimulation GetBoard(int playerIndex) => playerIndex == 0 ? _board1 : _board2;

    private static ReadOnlySpan<GameInput> Limit(ReadOnlySpan<GameInput> inputs)
        => inputs.Length <= MaxInputsPerTick ? inputs : inputs.Slice(0, MaxInputsPerTick);

    private void Judge()
    {
        if (!_board1.ToppedOut && !_board2.ToppedOut)
        {
            return;
        }

        if (_board1.ToppedOut && _board2.ToppedOut)
        {
            // Both died on the same tick: whoever was buried by incoming garbage lost to it.
            Outcome = _board1.ReceivedGarbageThisTick == _board2.ReceivedGarbageThisTick
                ? DuelOutcome.Draw
                : _board1.ReceivedGarbageThisTick
                    ? DuelOutcome.Player2Wins
                    : DuelOutcome.Player1Wins;
            return;
        }

        Outcome = _board1.ToppedOut ? DuelOutcome.Player2Wins : DuelOutcome.Player1Wins;
    }
}
