using UnitGenerator;

namespace SharpCampus.Shared;

// Single home for the target-framework split, so each value object declares its options in one line.
internal static class UnitOptions
{
    // EF Core is a server-side, net-only dependency: the Unity-facing netstandard build must compile
    // without the converter, while the net10.0 build the servers reference carries it.
    public const UnitGenerateOptions Persisted =
#if NET10_0_OR_GREATER
        UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.EntityFrameworkValueConverter;
#else
        UnitGenerateOptions.MessagePackFormatter;
#endif
}
