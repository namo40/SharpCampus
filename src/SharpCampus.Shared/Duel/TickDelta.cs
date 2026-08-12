using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// What one simulation tick changed on both boards. Sent every tick while a match is playing, even
/// when nothing happened, so a client can tell a quiet tick from a lost one.
/// </summary>
/// <param name="Tick">Tick this delta describes.</param>
/// <param name="Players">Per-seat state, in seat order.</param>
/// <param name="Events">Everything the simulation reported this tick, in the order it happened.</param>
[MessagePackObject]
public sealed record TickDelta(
    [property: Key(0)] TickNumber Tick,
    [property: Key(1)] PlayerDelta[] Players,
    [property: Key(2)] DuelEvent[] Events);
