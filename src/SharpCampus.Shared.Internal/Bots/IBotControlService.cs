using MagicOnion;

namespace SharpCampus.Shared.Internal.Bots;

/// <summary>
/// What the ApiServer asks of the bot server. Server to server only: never reachable from a game client.
/// </summary>
// Production note: this contract carries no caller authentication because it is only routed inside the
// cluster. A real deployment would pin it behind mTLS and a NetworkPolicy rather than trust the network.
public interface IBotControlService : IService<IBotControlService>
{
    /// <summary>
    /// Sends a bot into the match queue. The bot signs in as an account of its own and queues like any
    /// other player, so the call returns as soon as one is on its way rather than when it has signed in
    /// or been matched.
    /// </summary>
    /// <returns>Whether a bot was deployed.</returns>
    UnaryResult<SummonBotResult> SummonAsync();
}
