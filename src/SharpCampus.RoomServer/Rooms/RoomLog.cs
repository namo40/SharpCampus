using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Values;
using ZLogger;

namespace SharpCampus.RoomServer.Rooms;

internal static partial class RoomLog
{
    [ZLoggerMessage(LogLevel.Information, "Room {roomId} created with seed {seed}")]
    public static partial void RoomCreated(this ILogger logger, RoomId roomId, ulong seed);

    [ZLoggerMessage(LogLevel.Information, "Room {roomId} seated {playerIndex} as {displayName}")]
    public static partial void RoomSeated(this ILogger logger, RoomId roomId, int playerIndex, string displayName);

    [ZLoggerMessage(LogLevel.Information, "Room {roomId} starting in {countdownTicks} ticks")]
    public static partial void RoomStarting(this ILogger logger, RoomId roomId, int countdownTicks);

    [ZLoggerMessage(LogLevel.Information, "Room {roomId} finished at tick {tick}: {outcome} by {reason}")]
    public static partial void RoomFinished(
        this ILogger logger,
        RoomId roomId,
        int tick,
        DuelOutcome outcome,
        MatchEndReason reason);

    [ZLoggerMessage(LogLevel.Information, "Room {roomId} lost {playerIndex}, holding the seat for {graceTicks} ticks")]
    public static partial void RoomSeatLost(this ILogger logger, RoomId roomId, int playerIndex, int graceTicks);

    [ZLoggerMessage(LogLevel.Information, "Room {roomId} took {playerIndex} back")]
    public static partial void RoomSeatResumed(this ILogger logger, RoomId roomId, int playerIndex);

    [ZLoggerMessage(LogLevel.Information, "Room {roomId} rematching with seed {seed}")]
    public static partial void RoomRematching(this ILogger logger, RoomId roomId, ulong seed);

    [ZLoggerMessage(LogLevel.Information, "Room {roomId} rematch declined")]
    public static partial void RoomRematchDeclined(this ILogger logger, RoomId roomId);

    [ZLoggerMessage(LogLevel.Information, "Room {roomId} got no rematch answer from {playerIndex}: {reason}")]
    public static partial void RoomRematchUnanswered(this ILogger logger, RoomId roomId, int playerIndex, string reason);

    [ZLoggerMessage(LogLevel.Information, "Room {roomId} closed from {state}")]
    public static partial void RoomClosed(this ILogger logger, RoomId roomId, RoomState state);

    [ZLoggerMessage(LogLevel.Error, "Room {roomId} stopped ticking")]
    public static partial void RoomTickFaulted(this ILogger logger, Exception exception, RoomId roomId);

    [ZLoggerMessage(LogLevel.Information, "Registered {name} with {roomCount}/{capacity} rooms")]
    public static partial void RoomServerRegistered(this ILogger logger, string name, int roomCount, int capacity);

    [ZLoggerMessage(LogLevel.Warning, "A registry heartbeat could not reach Redis: {reason}")]
    public static partial void HeartbeatSkipped(this ILogger logger, string reason);

    [ZLoggerMessage(LogLevel.Error, "Match {matchId} was not settled: {reason}")]
    public static partial void MatchSettlementFailed(this ILogger logger, MatchId matchId, string reason);

    [ZLoggerMessage(LogLevel.Error, "Match {matchId} did not reach the daily missions: {reason}")]
    public static partial void MissionProgressFailed(this ILogger logger, MatchId matchId, string reason);

    [ZLoggerMessage(LogLevel.Error, "Match {matchId} did not reach the leaderboards: {reason}")]
    public static partial void LeaderboardPushFailed(this ILogger logger, MatchId matchId, string reason);
}
