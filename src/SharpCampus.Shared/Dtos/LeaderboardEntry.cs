using MessagePack;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// One place on a board. Carries no account identifier: a board says who is ahead, not who they are.
/// </summary>
/// <param name="Rank">Place on the board, counting from 1.</param>
/// <param name="DisplayName">Nickname of the account holding the place.</param>
/// <param name="Score">Rating or win count, depending on the board this came from.</param>
[MessagePackObject]
public sealed record LeaderboardEntry(
    [property: Key(0)] int Rank,
    [property: Key(1)] string DisplayName,
    [property: Key(2)] int Score);
