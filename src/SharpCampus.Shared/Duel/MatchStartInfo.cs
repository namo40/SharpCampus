using MessagePack;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// Sent once both seats are taken, at the start of the countdown.
/// </summary>
/// <param name="Players">Both seats, in seat order.</param>
/// <param name="CountdownTicks">Ticks between this message and the first simulated tick.</param>
/// <param name="NextCount">Upcoming pieces the server reveals per board.</param>
[MessagePackObject]
public sealed record MatchStartInfo(
    [property: Key(0)] MatchPlayerInfo[] Players,
    [property: Key(1)] int CountdownTicks,
    [property: Key(2)] int NextCount);
