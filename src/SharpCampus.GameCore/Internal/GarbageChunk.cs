namespace SharpCampus.GameCore.Internal;

// One received attack. Every row of a chunk shares the hole column, which is what makes a
// multi-row attack survivable with a single well.
internal record struct GarbageChunk(int Rows, int HoleColumn);
