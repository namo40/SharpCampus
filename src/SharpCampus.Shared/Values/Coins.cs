using UnitGenerator;

namespace SharpCampus.Shared.Values;

/// <summary>
/// Soft currency an account spends on cosmetic items.
/// </summary>
[UnitOf(typeof(int), UnitOptions.Persisted)]
public readonly partial struct Coins;
