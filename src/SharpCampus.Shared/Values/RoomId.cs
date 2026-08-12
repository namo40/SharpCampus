using UnitGenerator;

namespace SharpCampus.Shared.Values;

/// <summary>
/// Identifies a duel room. Issued by the pairing worker when a match is made.
/// </summary>
[UnitOf(typeof(Ulid), UnitOptions.Transported | UnitGenerateOptions.ParseMethod)]
public readonly partial struct RoomId;
