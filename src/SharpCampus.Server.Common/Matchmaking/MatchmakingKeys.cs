using SharpCampus.Shared.Identity;

namespace SharpCampus.Server.Common.Matchmaking;

// Both servers touch these: the ApiServer writes them when it places a pair, and the room that pair
// went into deletes them when it closes.
public static class MatchmakingKeys
{
    public static string Ticket(UserId userId) => $"mm:ticket:{userId}";

    public static string ActiveRoom(UserId userId) => $"room:active:{userId}";
}
