using MessagePack;
using SharpCampus.GameCore;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// The part of one board that can change every tick without producing an event.
/// </summary>
/// <param name="PlayerIndex">Seat this board belongs to.</param>
/// <param name="HasActivePiece">Whether a piece is falling. The remaining pose fields are meaningless when false.</param>
/// <param name="ActiveKind">Falling piece.</param>
/// <param name="ActiveRotation">Rotation state of the falling piece.</param>
/// <param name="ActiveX">Board column of the falling piece's rotation box origin. May sit outside the board.</param>
/// <param name="ActiveY">Board row of the falling piece's rotation box origin. May sit outside the board.</param>
/// <param name="SoftDropActive">Whether the player is holding soft drop.</param>
/// <param name="PendingGarbage">Garbage rows queued against this board.</param>
/// <param name="Combo">Consecutive locks that cleared lines.</param>
/// <param name="Level">Gravity level the board has reached.</param>
[MessagePackObject]
public sealed record PlayerDelta(
    [property: Key(0)] PlayerIndex PlayerIndex,
    [property: Key(1)] bool HasActivePiece,
    [property: Key(2)] PieceKind ActiveKind,
    [property: Key(3)] Rotation ActiveRotation,
    [property: Key(4)] int ActiveX,
    [property: Key(5)] int ActiveY,
    [property: Key(6)] bool SoftDropActive,
    [property: Key(7)] int PendingGarbage,
    [property: Key(8)] int Combo,
    [property: Key(9)] int Level);
