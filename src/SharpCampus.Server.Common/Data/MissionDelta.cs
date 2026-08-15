using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

// How far one mission's metric moved in a single match. Only what moved is worth a delta.
public readonly record struct MissionDelta(MissionId MissionId, int Amount);
