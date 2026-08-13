using Cysharp.Text;
using SharpCampus.Cli.Resources;
using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;

namespace SharpCampus.Cli.Duel;

// One frame is assembled in full and written once over the previous one, so nothing is ever drawn
// cell by cell and the two boards on screen always belong to the same tick.
internal sealed class DuelRenderer(int seat, string[] displayNames)
{
    public const int MinimumWidth = 80;
    public const int MinimumHeight = 26;

    // Erasing to the end of each line keeps a shorter frame from leaving the previous one's tail behind.
    public const string LineEnd = "\u001b[K\r\n";

    private const int Columns = DuelReplicaBoard.Width;
    private const int VisibleRows = 20;
    private const int InnerWidth = Columns * 2;
    private const int HeaderPrefixWidth = 3;
    private const int HudLabelWidth = 8;
    private const int NextCount = 5;
    private const int GarbageMeterCap = 8;

    // The rooms run a fixed tick rate; it is master data on the server and not part of the contract.
    private const int TicksPerSecond = 20;

    // Spawn-pose minis for the hold and next panels, one column per cell: five previews drawn with
    // the board's two-column cells would not fit beside the boards.
    private static readonly string[][] _pieceShapes = BuildPieceShapes();

    public string BuildFrame(DuelReplica replica)
    {
        var mine = replica.Boards[seat];
        var rival = replica.Boards[seat ^ 1];

        // The builder is a mutable struct passed by ref, which rules out a using declaration.
        var frame = ZString.CreateStringBuilder();
        try
        {
            AppendHeader(ref frame, Strings.BoardHeaderYou, displayNames[seat]);
            frame.Append("  ");
            AppendHeader(ref frame, Strings.BoardHeaderRival, displayNames[seat ^ 1]);
            frame.Append(LineEnd);

            for (var row = 0; row < VisibleRows; row++)
            {
                // Rows come bottom-up from the replica, and the hidden spawn rows are never drawn.
                var y = VisibleRows - 1 - row;

                AppendBoardRow(ref frame, mine, y);
                frame.Append("  ");
                AppendBoardRow(ref frame, rival, y);
                AppendHudRow(ref frame, replica, mine, row);
                frame.Append(LineEnd);
            }

            AppendBottom(ref frame);
            frame.Append("  ");
            AppendBottom(ref frame);
            frame.Append(LineEnd);
            frame.Append(LineEnd);
            frame.Append(Strings.KeyMap);
            frame.Append(LineEnd);

            return frame.ToString();
        }
        finally
        {
            frame.Dispose();
        }
    }

    private static void AppendHeader(ref Utf16ValueStringBuilder frame, string label, string name)
    {
        var text = Truncate(ZString.Concat(label, " ", name), InnerWidth - HeaderPrefixWidth);

        frame.Append("┌─ ");
        frame.Append(text);
        frame.Append(' ');

        for (var i = DisplayWidth(text) + HeaderPrefixWidth; i < InnerWidth; i++)
        {
            frame.Append('─');
        }

        frame.Append('┐');
    }

    private static void AppendBottom(ref Utf16ValueStringBuilder frame)
    {
        frame.Append('└');

        for (var i = 0; i < InnerWidth; i++)
        {
            frame.Append('─');
        }

        frame.Append('┘');
    }

    private static void AppendBoardRow(ref Utf16ValueStringBuilder frame, DuelReplicaBoard board, int y)
    {
        var active = board.ActivePiece;

        frame.Append('│');

        for (var x = 0; x < Columns; x++)
        {
            if (active is { } piece && Covers(piece, x, y))
            {
                frame.Append("[]");
                continue;
            }

            frame.Append(board.GetCell(x, y) switch
            {
                CellKind.Empty => ". ",
                CellKind.Garbage => "▒▒",
                _ => "██",
            });
        }

        frame.Append('│');
    }

    private static void AppendHudRow(ref Utf16ValueStringBuilder frame, DuelReplica replica, DuelReplicaBoard board, int row)
    {
        if (row is not (1 or 2 or 3 or 5 or 6 or 7 or 9 or 10 or 11 or 13))
        {
            return;
        }

        frame.Append("   ");

        switch (row)
        {
            case 1:
                frame.Append(Strings.HudHold);
                break;
            case 2 or 3 when board.Hold is { } hold:
                AppendPieceRow(ref frame, hold, row - 2);
                break;
            case 3:
                frame.Append('-');
                break;
            case 5:
                frame.Append(Strings.HudNext);
                break;
            case 6 or 7:
                for (var i = 0; i < board.Next.Count && i < NextCount; i++)
                {
                    if (i > 0)
                    {
                        frame.Append("  ");
                    }

                    AppendPieceRow(ref frame, board.Next[i], row - 6);
                }

                break;
            case 9:
                AppendHudLabel(ref frame, Strings.HudCombo);
                frame.Append(board.Combo);
                break;
            case 10:
                AppendHudLabel(ref frame, Strings.HudGarbage);
                for (var i = 0; i < board.PendingGarbage && i < GarbageMeterCap; i++)
                {
                    frame.Append('▓');
                }

                frame.Append(" (");
                frame.Append(board.PendingGarbage);
                frame.Append(')');
                break;
            case 11:
                AppendHudLabel(ref frame, Strings.HudLevel);
                frame.Append(board.Level);
                frame.Append("   ");
                AppendClock(ref frame, board.ElapsedTicks);
                break;
            case 13:
                frame.Append(StateText(replica));
                break;
        }
    }

    private static void AppendHudLabel(ref Utf16ValueStringBuilder frame, string label)
    {
        frame.Append(label);

        for (var i = DisplayWidth(label); i < HudLabelWidth; i++)
        {
            frame.Append(' ');
        }

        frame.Append(' ');
    }

    private static void AppendClock(ref Utf16ValueStringBuilder frame, int elapsedTicks)
    {
        var seconds = elapsedTicks / TicksPerSecond;

        frame.Append(seconds / 60);
        frame.Append(':');

        if (seconds % 60 < 10)
        {
            frame.Append('0');
        }

        frame.Append(seconds % 60);
    }

    // The room announces the match a countdown ahead of the first tick, and the replica sits on tick
    // zero for that whole stretch.
    private static string StateText(DuelReplica replica)
    {
        if (replica.Result is not null)
        {
            return Strings.StateFinished;
        }

        return replica.Tick.AsPrimitive() == 0 ? Strings.StateGetReady : string.Empty;
    }

    // Row 0 is the top of the two-row preview; a one-row piece sits on the bottom row.
    private static void AppendPieceRow(ref Utf16ValueStringBuilder frame, PieceKind kind, int row) =>
        frame.Append(_pieceShapes[Tetrominoes.IndexOf(kind)][row]);

    private static string[][] BuildPieceShapes()
    {
        var shapes = new string[7][];

        for (var i = 0; i < shapes.Length; i++)
        {
            var cells = Tetrominoes.GetCells((PieceKind)(i + 1), Rotation.Spawn);
            var minX = int.MaxValue;
            var minY = int.MaxValue;

            foreach (var cell in cells)
            {
                minX = Math.Min(minX, cell.X);
                minY = Math.Min(minY, cell.Y);
            }

            shapes[i] = [BuildPieceRow(cells, minX, minY + 1), BuildPieceRow(cells, minX, minY)];
        }

        return shapes;
    }

    private static string BuildPieceRow(ReadOnlySpan<PieceCell> cells, int minX, int y)
    {
        // Previews line up in a horizontal strip, so every row is padded to the widest piece.
        Span<char> row = [' ', ' ', ' ', ' '];

        foreach (var cell in cells)
        {
            if (cell.Y == y)
            {
                row[cell.X - minX] = '█';
            }
        }

        return new string(row);
    }

    private static bool Covers(ActivePieceView piece, int x, int y)
    {
        var cells = piece.Cells;

        for (var i = 0; i < cells.Length; i++)
        {
            if (piece.X + cells[i].X == x && piece.Y + cells[i].Y == y)
            {
                return true;
            }
        }

        return false;
    }

    private static string Truncate(string text, int maxWidth)
    {
        var width = 0;

        for (var i = 0; i < text.Length; i++)
        {
            width += CharWidth(text[i]);
            if (width > maxWidth)
            {
                return text[..i];
            }
        }

        return text;
    }

    private static int DisplayWidth(string text)
    {
        var width = 0;

        foreach (var c in text)
        {
            width += CharWidth(c);
        }

        return width;
    }

    // Terminal columns, not chars: the Korean and Japanese labels take two columns each, and the box
    // borders only line up if the padding counts columns. The ranges are the wide classes of Unicode
    // UAX #11 (https://www.unicode.org/reports/tr11/).
    private static int CharWidth(char c) => c is
        (>= '\u1100' and <= '\u115f')
        or (>= '\u2e80' and <= '\u303e')
        or (>= '\u3041' and <= '\u33ff')
        or (>= '\u3400' and <= '\u4dbf')
        or (>= '\u4e00' and <= '\u9fff')
        or (>= '\ua000' and <= '\ua4cf')
        or (>= '\uac00' and <= '\ud7a3')
        or (>= '\uf900' and <= '\ufaff')
        or (>= '\ufe30' and <= '\ufe6f')
        or (>= '\uff00' and <= '\uff60')
        or (>= '\uffe0' and <= '\uffe6')
        ? 2
        : 1;
}
