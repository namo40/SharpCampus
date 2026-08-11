namespace SharpCampus.GameCore;

// Values mirror the matching CellKind members so a locked piece maps to a cell by a plain cast.
// Shapes below are drawn in spawn orientation (flat side down).
public enum PieceKind : byte
{
    // ####
    I = 1,

    // ##
    // ##
    O = 2,

    // .#.
    // ###
    T = 3,

    // .##
    // ##.
    S = 4,

    // ##.
    // .##
    Z = 5,

    // #..
    // ###
    J = 6,

    // ..#
    // ###
    L = 7,
}
