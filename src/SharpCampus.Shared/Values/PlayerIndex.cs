using UnitGenerator;

namespace SharpCampus.Shared.Values;

/// <summary>
/// Seat in a duel, 0 or 1.
/// </summary>
// Validate runs from the constructor the generated MessagePack formatter deserialises through, so a
// seat that never existed cannot enter the process from the wire either.
[UnitOf(typeof(int), UnitGenerateOptions.MessagePackFormatter | UnitGenerateOptions.Validate)]
public readonly partial struct PlayerIndex
{
    private partial void Validate()
    {
        if (value is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A duel seat is 0 or 1.");
        }
    }
}
