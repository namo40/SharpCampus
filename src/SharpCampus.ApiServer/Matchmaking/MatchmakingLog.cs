using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using ZLogger;

namespace SharpCampus.ApiServer.Matchmaking;

internal static partial class MatchmakingLog
{
    [ZLoggerMessage(LogLevel.Information, "Matched {first} and {second} into room {roomId} on {serverName}")]
    public static partial void MatchMade(
        this ILogger logger,
        UserId first,
        UserId second,
        RoomId roomId,
        string serverName);

    [ZLoggerMessage(LogLevel.Warning, "No room server available: {first} and {second} stay queued")]
    public static partial void NoRoomServer(this ILogger logger, UserId first, UserId second);

    [ZLoggerMessage(LogLevel.Warning, "Room {roomId} on {serverName} was refused ({outcome}): {first} and {second} stay queued")]
    public static partial void RoomRefused(
        this ILogger logger,
        RoomId roomId,
        string serverName,
        string outcome,
        UserId first,
        UserId second);

    [ZLoggerMessage(LogLevel.Information, "Summoned a bot for {userId}, alone in the queue for {waitedSeconds}s")]
    public static partial void BotSummoned(this ILogger logger, UserId userId, int waitedSeconds);

    [ZLoggerMessage(LogLevel.Warning, "No bot left for {userId}, alone in the queue for {waitedSeconds}s")]
    public static partial void BotExhausted(this ILogger logger, UserId userId, int waitedSeconds);

    [ZLoggerMessage(LogLevel.Warning, "Bot server unreachable for {userId}, alone in the queue for {waitedSeconds}s")]
    public static partial void BotSummonFailed(this ILogger logger, UserId userId, int waitedSeconds);

    [ZLoggerMessage(LogLevel.Warning, "A pairing pass could not reach Redis: {reason}")]
    public static partial void PairingPassSkipped(this ILogger logger, string reason);
}
