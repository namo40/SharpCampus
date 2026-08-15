#if NET10_0_OR_GREATER
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
#endif
using UnitGenerator;

namespace SharpCampus.Shared.Values;

/// <summary>
/// Identifies a single game. A room issues a new one for every game it runs, rematches included.
/// </summary>
[UnitOf(typeof(Ulid), UnitOptions.Transported | UnitGenerateOptions.ParseMethod)]
public readonly partial struct MatchId
{
#if NET10_0_OR_GREATER
    /// <summary>
    /// Stores the id as its canonical 26 character text. Written by hand rather than generated because
    /// UnitGenerator's converter would hand the provider a <see cref="Ulid"/>, which Npgsql cannot map.
    /// </summary>
    public sealed class MatchIdValueConverter()
        : ValueConverter<MatchId, string>(id => id.AsPrimitive().ToString(), text => Parse(text));
#endif
}
