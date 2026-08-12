using UnitGenerator;

namespace SharpCampus.Shared;

// Single home for the target-framework split, so each value object declares its options in one line.
internal static class UnitOptions
{
    // MessagePack carries these over the wire and into the master data binary, JSON carries them
    // through the master data source file and the transcoded HTTP endpoints. EF Core is the odd one
    // out: a server-side, net-only dependency, so the Unity-facing netstandard build must compile
    // without the converter while the net10.0 build the servers reference carries it.
    public const UnitGenerateOptions Persisted =
#if NET10_0_OR_GREATER
        UnitGenerateOptions.MessagePackFormatter
        | UnitGenerateOptions.JsonConverter
        | UnitGenerateOptions.EntityFrameworkValueConverter;
#else
        UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.JsonConverter;
#endif

    // For values that travel and get logged but are not columns. The generated JSON converter hands off
    // to the underlying type's own converter, and Ulid only ships one on .NET, so the Unity-facing build
    // leaves JSON out rather than generating a converter that would throw the first time it ran.
    public const UnitGenerateOptions Transported =
#if NET10_0_OR_GREATER
        UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.JsonConverter;
#else
        UnitGenerateOptions.MessagePackFormatter;
#endif
}
