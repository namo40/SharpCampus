using MessagePipe;
using SharpCampus.Server.Common.Leaderboards;
using SharpCampus.Server.Common.Settlement;

namespace SharpCampus.RoomServer.Rooms;

// Runs off a room's loop thread with no request behind it, so it opens the scope the pooled DbContext
// needs itself.
internal sealed class MatchSettlementHandler(
    IServiceScopeFactory scopes,
    ILeaderboardStore leaderboard,
    TimeProvider time,
    ILogger<MatchSettlementHandler> logger) : IAsyncMessageHandler<MatchFinishedEvent>
{
    public async ValueTask HandleAsync(MatchFinishedEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        MatchSettlement settlement;

        try
        {
            settlement = await scope.ServiceProvider
                .GetRequiredService<IMatchSettlementService>()
                .SettleAsync(ToRequest(message));
        }
        catch (Exception e)
        {
            // Production note: a settlement that fails is money and rating the players never see again.
            // A real service would write the event to an outbox and retry it from there.
            logger.MatchSettlementFailed(message.MatchId, e.Message);
            return;
        }

        try
        {
            await PushAsync(settlement);
        }
        catch (Exception e)
        {
            // Production note: the profiles are already committed, so only the derived board is behind.
            // A real service would rebuild it from the match records rather than let it drift.
            logger.LeaderboardPushFailed(message.MatchId, e.Message);
        }
    }

    private static MatchSettlementRequest ToRequest(MatchFinishedEvent message) => new(
        message.MatchId,
        message.Outcome,
        message.Reason,
        message.DurationTicks,
        new MatchSettlementPlayer(message.Player1.UserId, message.Player1.Stats),
        new MatchSettlementPlayer(message.Player2.UserId, message.Player2.Stats));

    private async Task PushAsync(MatchSettlement settlement)
    {
        // The whole match counts towards one day, the one it ended on.
        var date = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

        await PushAsync(settlement.Player1, date);
        await PushAsync(settlement.Player2, date);
    }

    private async Task PushAsync(SettledPlayer player, DateOnly date)
    {
        await leaderboard.SetRatingAsync(player.UserId, player.Rating);

        // A draw is nobody's win, so the day's board hears nothing about it.
        if (player.Record.Outcome == SettlementCalculator.Win)
        {
            await leaderboard.AddDailyWinAsync(player.UserId, date);
        }
    }
}
