using MessagePack;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// A board as one answer: its leading places and where the caller stands on it.
/// </summary>
/// <param name="Top">The first 100 places, best first. Empty while nothing has been settled onto the board.</param>
/// <param name="Me">
/// The caller's own place, filled whenever the caller is on the board at all, including when the same
/// place is already in <paramref name="Top"/>. The rank is the global one, so it can run past 100.
/// Null when the caller has never reached this board.
/// </param>
[MessagePackObject]
public sealed record LeaderboardView(
    [property: Key(0)] LeaderboardEntry[] Top,
    [property: Key(1)] LeaderboardEntry? Me);
