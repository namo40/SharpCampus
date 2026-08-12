using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using ZLogger;

namespace SharpCampus.RoomServer.Rooms;

internal static partial class RoomLog
{
    [ZLoggerMessage(LogLevel.Information, "Room {roomKey} created with seed {seed}")]
    public static partial void RoomCreated(this ILogger logger, string roomKey, ulong seed);

    [ZLoggerMessage(LogLevel.Information, "Room {roomKey} seated {playerIndex} as {displayName}")]
    public static partial void RoomSeated(this ILogger logger, string roomKey, int playerIndex, string displayName);

    [ZLoggerMessage(LogLevel.Information, "Room {roomKey} starting in {countdownTicks} ticks")]
    public static partial void RoomStarting(this ILogger logger, string roomKey, int countdownTicks);

    [ZLoggerMessage(LogLevel.Information, "Room {roomKey} finished at tick {tick}: {outcome} by {reason}")]
    public static partial void RoomFinished(
        this ILogger logger,
        string roomKey,
        int tick,
        DuelOutcome outcome,
        MatchEndReason reason);

    [ZLoggerMessage(LogLevel.Information, "Room {roomKey} closed from {state}")]
    public static partial void RoomClosed(this ILogger logger, string roomKey, RoomState state);

    [ZLoggerMessage(LogLevel.Error, "Room {roomKey} stopped ticking")]
    public static partial void RoomTickFaulted(this ILogger logger, Exception exception, string roomKey);
}
