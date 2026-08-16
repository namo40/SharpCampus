namespace SharpCampus.Server.Common.Leaderboards;

// The RoomServer writes these when a match settles and the ApiServer reads them back. The date in the
// daily key is what makes the reset free: a new day is a new key, and nothing has to clear the old one.
public static class LeaderboardKeys
{
    public const string Rating = "lb:rating";

    public static string Daily(DateOnly date) => $"lb:daily:{date:yyyyMMdd}";

    // The assembled page of a board, under a key of its own: dropping a cached page must never be able
    // to touch the settled places it was built from.
    public static string Cache(string board) => $"lbc:{board}";
}
