using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Values;
using ZLogger;

namespace SharpCampus.BotServer.Bots;

internal static partial class BotLog
{
    [ZLoggerMessage(LogLevel.Information, "Bot {nickname} summoned")]
    public static partial void BotSummoned(this ILogger logger, string nickname);

    [ZLoggerMessage(LogLevel.Warning, "All {poolSize} bot accounts are already playing")]
    public static partial void BotPoolExhausted(this ILogger logger, int poolSize);

    [ZLoggerMessage(LogLevel.Information, "Bot {nickname} joined the queue")]
    public static partial void BotQueued(this ILogger logger, string nickname);

    [ZLoggerMessage(LogLevel.Information, "Bot {nickname} left the queue after {waitedSeconds}s with nobody to play")]
    public static partial void BotQueueTimedOut(this ILogger logger, string nickname, int waitedSeconds);

    [ZLoggerMessage(LogLevel.Information, "Bot {nickname} matched into room {roomId} on {endpoint}")]
    public static partial void BotMatched(this ILogger logger, string nickname, RoomId roomId, string endpoint);

    [ZLoggerMessage(LogLevel.Information, "Bot {nickname} took seat {playerIndex} in room {roomId}")]
    public static partial void BotSeated(this ILogger logger, string nickname, int playerIndex, RoomId roomId);

    [ZLoggerMessage(LogLevel.Warning, "Bot {nickname} was not seated in room {roomId}")]
    public static partial void BotSeatRejected(this ILogger logger, string nickname, RoomId roomId);

    [ZLoggerMessage(LogLevel.Information, "Bot {nickname} finished at tick {tick}: {outcome} by {reason}")]
    public static partial void BotMatchFinished(
        this ILogger logger,
        string nickname,
        int tick,
        DuelOutcome outcome,
        MatchEndReason reason);

    [ZLoggerMessage(LogLevel.Information, "Bot {nickname} released")]
    public static partial void BotReleased(this ILogger logger, string nickname);

    [ZLoggerMessage(LogLevel.Error, "Bot {nickname} could not sign in: {reason}")]
    public static partial void BotAuthFailed(this ILogger logger, string nickname, string reason);

    [ZLoggerMessage(LogLevel.Error, "Bot {nickname} stopped: {reason}")]
    public static partial void BotFailed(this ILogger logger, string nickname, string reason);
}
