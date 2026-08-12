using UnitGenerator;

namespace SharpCampus.Shared.Values;

/// <summary>
/// Identifies a cosmetic block theme. Matches the primary key of the skin master data table.
/// </summary>
// Comparable alone would also emit relational operators, which do not compile over a string; the
// master data index still needs IComparable to sort and binary-search its keys.
[UnitOf(
    typeof(string),
    UnitOptions.Persisted | UnitGenerateOptions.Comparable | UnitGenerateOptions.WithoutComparisonOperator)]
public readonly partial struct SkinId;
