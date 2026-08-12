using UnitGenerator;

namespace SharpCampus.Shared.Values;

/// <summary>
/// Position of a simulation tick in a match, counted from the first tick after the countdown.
/// </summary>
// Wire-only: ticks never reach the database or a JSON payload, so this carries the MessagePack
// formatter alone. Comparable is what lets a client tell a stale delta from the next one.
[UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Comparable)]
public readonly partial struct TickNumber;
