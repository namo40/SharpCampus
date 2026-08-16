using MemoryPack;

namespace SharpCampus.Server.Common.Leaderboards;

// The cache's own wire format, mirroring the contract DTO rather than reusing it: MemoryPack needs
// .NET 7 or newer and SharpCampus.Shared is built for netstandard2.1, so what crosses the wire to a
// client and what sits in Redis between servers are serialized by different libraries on purpose.
[MemoryPackable]
internal sealed partial record CachedLeaderboardEntry(int Rank, string Nickname, int Score);
