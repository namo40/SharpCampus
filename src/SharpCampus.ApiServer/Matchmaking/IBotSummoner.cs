using SharpCampus.Shared.Internal.Bots;

namespace SharpCampus.ApiServer.Matchmaking;

public interface IBotSummoner
{
    // Null when the bot server cannot be reached, which the caller has to survive: a queue with nobody
    // to pair is still a working queue.
    Task<SummonBotResult?> SummonAsync();
}
