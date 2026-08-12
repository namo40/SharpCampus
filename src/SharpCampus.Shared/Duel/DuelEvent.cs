using MessagePack;
using SharpCampus.GameCore;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// One simulation event on the wire. The payload stays flat, exactly as
/// <see cref="TickEvent"/> packs it, so no per-kind message shape has to be kept in step with the simulation.
/// </summary>
/// <param name="Kind">Which event this is.</param>
/// <param name="PlayerIndex">Seat the event belongs to.</param>
/// <param name="Value">First payload word, read through the accessors of <see cref="TickEvent"/>.</param>
/// <param name="Extra">Second payload word, read through the accessors of <see cref="TickEvent"/>.</param>
[MessagePackObject]
public sealed record DuelEvent(
    [property: Key(0)] TickEventKind Kind,
    [property: Key(1)] PlayerIndex PlayerIndex,
    [property: Key(2)] int Value,
    [property: Key(3)] int Extra)
{
    /// <summary>Copies a simulation event onto the wire.</summary>
    /// <param name="tickEvent">Event the simulation produced.</param>
    /// <returns>The same event as a serializable record.</returns>
    public static DuelEvent From(TickEvent tickEvent)
        => new(tickEvent.Kind, new PlayerIndex(tickEvent.PlayerIndex), tickEvent.Value, tickEvent.Extra);

    /// <summary>Reads the event back as the simulation's own struct, for its payload accessors.</summary>
    /// <returns>The event in simulation form.</returns>
    public TickEvent ToTickEvent() => new(Kind, PlayerIndex.AsPrimitive(), Value, Extra);
}
