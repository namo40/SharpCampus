using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// Both boards in full, as of one tick. Requested on entry and whenever a client notices a gap in
/// the delta stream.
/// </summary>
/// <param name="Tick">Tick the boards were read at. Deltas up to and including this tick are already reflected.</param>
/// <param name="Players">Per-seat boards, in seat order.</param>
[MessagePackObject]
public sealed record DuelSnapshot(
    [property: Key(0)] TickNumber Tick,
    [property: Key(1)] PlayerSnapshot[] Players);
