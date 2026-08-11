using SharpCampus.GameCore.Internal;
using Xunit;

namespace SharpCampus.GameCore.Tests;

internal sealed class ScriptedPieceSource(params PieceKind[] sequence) : IPieceSource
{
    private int _index;

    public PieceKind Next()
    {
        var kind = sequence[_index];
        _index = (_index + 1) % sequence.Length;
        return kind;
    }
}

internal static class TestBoard
{
    private const ulong Seed = 0x5EEDUL;

    public static BoardSimulation Create(SimulationConfig config, params PieceKind[] pieces)
        => new(config, new ScriptedPieceSource(pieces), Seed, 0);

    public static DuelSimulation CreateDuel(SimulationConfig config, params PieceKind[] pieces)
        => new(config, new ScriptedPieceSource(pieces), new ScriptedPieceSource(pieces), Seed);

    public static void Advance(this BoardSimulation board, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            board.Tick([]);
        }
    }

    public static int RunUntil(this BoardSimulation board, TickEventKind kind, int maxTicks)
    {
        for (var tick = 1; tick <= maxTicks; tick++)
        {
            board.Tick([]);
            foreach (var tickEvent in board.LastTickEvents)
            {
                if (tickEvent.Kind == kind)
                {
                    return tick;
                }
            }
        }

        return -1;
    }

    public static ActivePieceView Active(this BoardSimulation board)
    {
        var active = board.View.ActivePiece;
        Assert.NotNull(active);
        return active.Value;
    }

    public static bool Has(this IReadOnlyList<TickEvent> events, TickEventKind kind)
    {
        foreach (var tickEvent in events)
        {
            if (tickEvent.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    public static TickEvent Single(this IReadOnlyList<TickEvent> events, TickEventKind kind)
    {
        var matches = new List<TickEvent>();
        foreach (var tickEvent in events)
        {
            if (tickEvent.Kind == kind)
            {
                matches.Add(tickEvent);
            }
        }

        return Assert.Single(matches);
    }

    public static List<TickEvent> All(this IReadOnlyList<TickEvent> events, TickEventKind kind)
    {
        var matches = new List<TickEvent>();
        foreach (var tickEvent in events)
        {
            if (tickEvent.Kind == kind)
            {
                matches.Add(tickEvent);
            }
        }

        return matches;
    }
}

internal static class BoardFixture
{
    public static CellKind[] Empty() => new CellKind[BoardSimulation.Width * BoardSimulation.Height];

    public static CellKind[] FillRow(this CellKind[] cells, int y, params int[] holeColumns)
    {
        for (var x = 0; x < BoardSimulation.Width; x++)
        {
            if (Array.IndexOf(holeColumns, x) < 0)
            {
                cells[(y * BoardSimulation.Width) + x] = CellKind.Garbage;
            }
        }

        return cells;
    }

    public static CellKind[] FillRows(this CellKind[] cells, int fromY, int toY, params int[] holeColumns)
    {
        for (var y = fromY; y <= toY; y++)
        {
            cells.FillRow(y, holeColumns);
        }

        return cells;
    }

    public static CellKind[] FillColumn(this CellKind[] cells, int x, int fromY, int toY)
    {
        for (var y = fromY; y <= toY; y++)
        {
            cells[(y * BoardSimulation.Width) + x] = CellKind.Garbage;
        }

        return cells;
    }
}
