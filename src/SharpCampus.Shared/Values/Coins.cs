using UnitGenerator;

namespace SharpCampus.Shared.Values;

/// <summary>
/// Soft currency an account spends on cosmetic items.
/// </summary>
// Arithmetic and comparison exist so the shop's debit is a translatable EF expression: the balance
// check and the subtraction both run inside the database.
[UnitOf(typeof(int), UnitOptions.Persisted | UnitGenerateOptions.ArithmeticOperator | UnitGenerateOptions.Comparable)]
public readonly partial struct Coins;
