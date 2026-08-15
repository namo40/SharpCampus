using MessagePipe;
using SharpCampus.GameCore;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Missions;

namespace SharpCampus.RoomServer.Rooms;

// The second subscriber to the same finished match: settlement pays for the game, this counts it
// towards the day. Neither knows about the other, and a room publishes once for both.
internal sealed class MissionProgressHandler(
    IServiceScopeFactory scopes,
    MemoryDatabase masterData,
    TimeProvider time,
    ILogger<MissionProgressHandler> logger) : IAsyncMessageHandler<MatchFinishedEvent>
{
    public async ValueTask HandleAsync(MatchFinishedEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        try
        {
            var missions = scope.ServiceProvider.GetRequiredService<IMissionRepository>();

            // The whole match counts towards one day, the one it ended on.
            var date = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

            await RecordAsync(missions, date, message.Player1, message.Outcome == DuelOutcome.Player1Wins);
            await RecordAsync(missions, date, message.Player2, message.Outcome == DuelOutcome.Player2Wins);
        }
        catch (Exception e)
        {
            // Production note: progress dropped here is a day the player has to play again. Draining the
            // event from an outbox instead would let the write be retried.
            logger.MissionProgressFailed(message.MatchId, e.Message);
        }
    }

    private static int Amount(MissionMetric metric, BoardStats stats, bool won) => metric switch
    {
        MissionMetric.MatchesPlayed => 1,
        MissionMetric.Wins => won ? 1 : 0,
        MissionMetric.LinesCleared => stats.LinesCleared,
        MissionMetric.GarbageSent => stats.GarbageSent,
        MissionMetric.HardDrops => stats.HardDrops,
        _ => 0,
    };

    private Task RecordAsync(IMissionRepository missions, DateOnly date, MatchParticipant participant, bool won)
    {
        List<MissionDelta> deltas = [];

        foreach (var mission in masterData.MissionTable.All)
        {
            var amount = Amount(mission.Metric, participant.Stats, won);
            if (amount > 0)
            {
                deltas.Add(new MissionDelta(mission.MissionId, amount));
            }
        }

        return deltas.Count == 0 ? Task.CompletedTask : missions.RecordAsync(participant.UserId, date, deltas);
    }
}
