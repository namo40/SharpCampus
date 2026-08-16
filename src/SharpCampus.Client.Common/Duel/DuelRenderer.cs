using Cysharp.Text;
using SharpCampus.Client.Common.Resources;
using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.MasterData;

namespace SharpCampus.Client.Common.Duel;

// One frame is assembled in full and written once over the previous one, so nothing is ever drawn
// cell by cell and the two boards on screen always belong to the same tick. Each board is drawn in
// its own owner's skin, so a bought skin is something the opponent sees too.
public sealed class DuelRenderer(int seat, string[] displayNames, Skin[] skins, DuelStatus status)
{
    public const int MinimumWidth = 80;
    public const int MinimumHeight = 26;

    // Erasing to the end of each line keeps a shorter frame from leaving the previous one's tail behind.
    public const string LineEnd = "\u001b[K\r\n";

    private const string Reset = "\u001b[0m";

    // The falling piece keeps a shape of its own whatever skin the board wears, so it stays apart from
    // what has already landed.
    private const string ActiveGlyph = "[]";
    private const string EmptyGlyph = ". ";

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

    // Foreground sequences in ConsoleColor's own member order: the eight base colors are 30-37 and
    // their bright counterparts 90-97 (https://en.wikipedia.org/wiki/ANSI_escape_code).
    private static readonly string[] _foregrounds =
    [
        "\u001b[30m", "\u001b[34m", "\u001b[32m", "\u001b[36m", "\u001b[31m", "\u001b[35m", "\u001b[33m", "\u001b[37m",
        "\u001b[90m", "\u001b[94m", "\u001b[92m", "\u001b[96m", "\u001b[91m", "\u001b[95m", "\u001b[93m", "\u001b[97m",
    ];

    // Both parsed up front: a frame is built twenty times a second and must not touch a color name again.
    private readonly string[][] _cellColors = BuildCellColors(skins);
    private readonly string[] _blockGlyphs = [skins[0].BlockGlyph, skins[1].BlockGlyph];

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

                AppendBoardRow(ref frame, mine, seat, y);
                frame.Append("  ");
                AppendBoardRow(ref frame, rival, seat ^ 1, y);
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

    private void AppendBoardRow(ref Utf16ValueStringBuilder frame, DuelReplicaBoard board, int boardSeat, int y)
    {
        var active = board.ActivePiece;
        var colors = _cellColors[boardSeat];
        var glyph = _blockGlyphs[boardSeat];
        var painted = string.Empty;

        frame.Append('│');

        for (var x = 0; x < Columns; x++)
        {
            if (active is { } piece && Covers(piece, x, y))
            {
                AppendCell(ref frame, ref painted, colors[(int)piece.Kind], ActiveGlyph);
                continue;
            }

            var cell = board.GetCell(x, y);
            AppendCell(ref frame, ref painted, colors[(int)cell], cell == CellKind.Empty ? EmptyGlyph : glyph);
        }

        // The border belongs to the frame, not to the board, so the row hands the terminal its own color back.
        if (painted.Length > 0)
        {
            frame.Append(Reset);
        }

        frame.Append('│');
    }

    // A color code goes in only where the color changes, which keeps a frame from carrying one per cell.
    private static void AppendCell(ref Utf16ValueStringBuilder frame, ref string painted, string color, string glyph)
    {
        if (painted != color)
        {
            frame.Append(color.Length > 0 ? color : Reset);
            painted = color;
        }

        frame.Append(glyph);
    }

    private void AppendHudRow(ref Utf16ValueStringBuilder frame, DuelReplica replica, DuelReplicaBoard board, int row)
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
                frame.Append("  ");
                AppendPing(ref frame);
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

    // What the heartbeat measured, which is the connection's own round trip and not the tick stream's.
    private void AppendPing(ref Utf16ValueStringBuilder frame)
    {
        frame.Append(Strings.HudPing);
        frame.Append(' ');

        var ping = status.PingMilliseconds;
        if (ping < 0)
        {
            frame.Append("--");
            return;
        }

        frame.Append(ping);
        frame.Append("ms");
    }

    // The room announces the match a countdown ahead of the first tick, and the replica sits on tick
    // zero for that whole stretch. A rival who dropped out keeps their board: the match runs on while
    // the room holds their seat, so the line says so rather than the board going blank.
    private string StateText(DuelReplica replica)
    {
        if (replica.Result is not null)
        {
            return Strings.StateFinished;
        }

        if (!status.IsConnected(seat ^ 1))
        {
            return Strings.OpponentDisconnected;
        }

        return replica.Tick.AsPrimitive() == 0 ? Strings.StateGetReady : string.Empty;
    }

    // Row 0 is the top of the two-row preview; a one-row piece sits on the bottom row. The panels are
    // this player's own, so they are drawn in this player's colors whichever board is beside them.
    private void AppendPieceRow(ref Utf16ValueStringBuilder frame, PieceKind kind, int row)
    {
        frame.Append(_cellColors[seat][(int)kind]);
        frame.Append(_pieceShapes[Tetrominoes.IndexOf(kind)][row]);
        frame.Append(Reset);
    }

    // Indexed by CellKind: the palette runs I, O, T, S, Z, J, L, Garbage, which is CellKind's own order
    // one place along, and an empty cell is left to the terminal's color.
    private static string[][] BuildCellColors(Skin[] skins)
    {
        var colors = new string[skins.Length][];

        for (var i = 0; i < skins.Length; i++)
        {
            var palette = skins[i].PaletteColors();
            var seatColors = new string[palette.Length + 1];
            seatColors[(int)CellKind.Empty] = string.Empty;

            for (var color = 0; color < palette.Length; color++)
            {
                seatColors[color + 1] = _foregrounds[(int)Enum.Parse<ConsoleColor>(palette[color])];
            }

            colors[i] = seatColors;
        }

        return colors;
    }

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
