using MessagePack;
using SharpCampus.GameCore;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// Everything needed to rebuild one board from nothing.
/// </summary>
/// <param name="PlayerIndex">Seat this board belongs to.</param>
/// <param name="Cells">Settled cells as <see cref="CellKind"/> bytes, row-major, index = (y * 10) + x, y counted from the bottom. The falling piece is not included.</param>
/// <param name="Next">Upcoming pieces, soonest first.</param>
/// <param name="HasHold">Whether a piece is being held.</param>
/// <param name="Hold">Held piece, meaningless when <paramref name="HasHold"/> is false.</param>
/// <param name="HoldAvailable">Whether the current piece may still be swapped into hold.</param>
/// <param name="HasActivePiece">Whether a piece is falling.</param>
/// <param name="ActiveKind">Falling piece.</param>
/// <param name="ActiveRotation">Rotation state of the falling piece.</param>
/// <param name="ActiveX">Board column of the falling piece's rotation box origin.</param>
/// <param name="ActiveY">Board row of the falling piece's rotation box origin.</param>
/// <param name="SoftDropActive">Whether the player is holding soft drop.</param>
/// <param name="PendingGarbage">Garbage rows queued against this board.</param>
/// <param name="Combo">Consecutive locks that cleared lines.</param>
/// <param name="Level">Gravity level the board has reached.</param>
/// <param name="ElapsedTicks">Ticks this board has simulated.</param>
/// <param name="ToppedOut">Whether this board has lost.</param>
[MessagePackObject]
public sealed record PlayerSnapshot(
    [property: Key(0)] PlayerIndex PlayerIndex,
    [property: Key(1)] byte[] Cells,
    [property: Key(2)] PieceKind[] Next,
    [property: Key(3)] bool HasHold,
    [property: Key(4)] PieceKind Hold,
    [property: Key(5)] bool HoldAvailable,
    [property: Key(6)] bool HasActivePiece,
    [property: Key(7)] PieceKind ActiveKind,
    [property: Key(8)] Rotation ActiveRotation,
    [property: Key(9)] int ActiveX,
    [property: Key(10)] int ActiveY,
    [property: Key(11)] bool SoftDropActive,
    [property: Key(12)] int PendingGarbage,
    [property: Key(13)] int Combo,
    [property: Key(14)] int Level,
    [property: Key(15)] int ElapsedTicks,
    [property: Key(16)] bool ToppedOut);
