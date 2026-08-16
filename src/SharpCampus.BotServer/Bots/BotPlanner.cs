using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;

namespace SharpCampus.BotServer.Bots;

// A one-move heuristic: every drop the falling piece can reach is scored, the best one wins, and the
// inputs that get there are handed back. No hold, no soft drop, no look at what comes next.
internal static class BotPlanner
{
    // Weights of our own, settled by watching it play: keep the stack low and level, never bury a
    // cell, and take a line when one is going.
    private const double HeightWeight = 0.5;
    private const double HoleWeight = 0.7;
    private const double BumpinessWeight = 0.2;
    private const double LineWeight = 0.8;

    public static GameInput[] Plan(DuelReplicaBoard board)
        => board.ActivePiece is { } piece ? Plan(board.Cells, piece.Kind) : [];

    public static GameInput[] Plan(ReadOnlySpan<CellKind> cells, PieceKind kind)
    {
        var candidates = Placements.Enumerate(cells, kind);
        if (candidates.Count == 0)
        {
            return [];
        }

        var best = candidates[0];
        var bestScore = Score(best);

        for (var i = 1; i < candidates.Count; i++)
        {
            var score = Score(candidates[i]);
            if (score > bestScore)
            {
                best = candidates[i];
                bestScore = score;
            }
        }

        return Inputs(kind, best);
    }

    private static double Score(Placement placement)
        => (LineWeight * placement.LinesCleared)
            - (HeightWeight * placement.After.AggregateHeight)
            - (HoleWeight * placement.After.Holes)
            - (BumpinessWeight * placement.After.Bumpiness);

    // Rotation first: a piece turns where it spawns, high enough that no wall kick can shift it
    // sideways, so the walk to the target column always starts from the spawn column.
    private static GameInput[] Inputs(PieceKind kind, Placement placement)
    {
        var inputs = new List<GameInput>();

        switch (placement.Rotation)
        {
            case Rotation.Right:
                inputs.Add(GameInput.RotateCw);
                break;
            case Rotation.Half:
                inputs.Add(GameInput.RotateCw);
                inputs.Add(GameInput.RotateCw);
                break;
            case Rotation.Left:
                inputs.Add(GameInput.RotateCcw);
                break;
        }

        var steps = placement.X - Tetrominoes.GetSpawnX(kind);
        var move = steps < 0 ? GameInput.MoveLeft : GameInput.MoveRight;
        for (var i = 0; i < Math.Abs(steps); i++)
        {
            inputs.Add(move);
        }

        inputs.Add(GameInput.HardDrop);
        return [.. inputs];
    }
}
