using UnitGenerator;

namespace SharpCampus.Shared.Values;

/// <summary>
/// Skill score matchmaking pairs opponents by. New accounts start at 1000.
/// </summary>
[UnitOf(typeof(int), UnitOptions.Persisted)]
public readonly partial struct Rating;
