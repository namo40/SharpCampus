namespace SharpCampus.GameCore;

// How a settled board scores on the three shapes a placement heuristic cares about. Column height is
// the row above the topmost filled cell, so a column with a covered gap is as tall as its cover.
public readonly record struct BoardMetrics(int AggregateHeight, int Holes, int Bumpiness);
