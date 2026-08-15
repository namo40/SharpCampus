using MessagePipe;
using SharpCampus.Server.Common.Settlement;

namespace SharpCampus.RoomServer.Rooms;

// Runs off a room's loop thread with no request behind it, so it opens the scope the pooled DbContext
// needs itself.
internal sealed class MatchSettlementHandler(
    IServiceScopeFactory scopes,
    ILogger<MatchSettlementHandler> logger) : IAsyncMessageHandler<MatchFinishedEvent>
{
    public async ValueTask HandleAsync(MatchFinishedEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        try
        {
            await scope.ServiceProvider.GetRequiredService<IMatchSettlementService>().SettleAsync(ToRequest(message));
        }
        catch (Exception e)
        {
            // Production note: a settlement that fails is money and rating the players never see again.
            // A real service would write the event to an outbox and retry it from there.
            logger.MatchSettlementFailed(message.MatchId, e.Message);
        }
    }

    private static MatchSettlementRequest ToRequest(MatchFinishedEvent message) => new(
        message.MatchId,
        message.Outcome,
        message.Reason,
        message.DurationTicks,
        new MatchSettlementPlayer(message.Player1.UserId, message.Player1.Stats),
        new MatchSettlementPlayer(message.Player2.UserId, message.Player2.Stats));
}
