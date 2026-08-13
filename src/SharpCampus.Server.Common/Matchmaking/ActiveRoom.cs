using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Matchmaking;

// Where a player is currently playing. Enough for a client that died mid-match to be sent straight
// back instead of into the queue.
public sealed record ActiveRoom(RoomId RoomId, string Endpoint);
