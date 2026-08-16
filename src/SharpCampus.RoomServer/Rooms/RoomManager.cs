using System.Collections.Concurrent;
using System.Diagnostics;
using Cysharp.Threading;
using MessagePipe;
using Microsoft.Extensions.Options;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.RoomServer.Observability;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Server.Common.Rooms;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Values;

namespace SharpCampus.RoomServer.Rooms;

public sealed class RoomManager(
    ILogicLooperPool loopers,
    DuelRules rules,
    MemoryDatabase masterData,
    IActiveRoomStore activeRooms,
    IRoomLocationStore locations,
    IOptions<RoomServerOptions> options,
    IAsyncPublisher<MatchFinishedEvent> matchFinished,
    RoomMetrics metrics,
    ILogger<RoomManager> logger)
{
    private readonly ConcurrentDictionary<RoomId, DuelRoom> _rooms = new();

    // Creation registers a loop action and has to respect the capacity it just read, so it is serialised;
    // lookups stay lock free.
    private readonly Lock _createGate = new();

    // ReSharper disable once InconsistentlySynchronizedField
    internal int RoomCount => _rooms.Count;

    // Rooms only ever come into being through the ApiServer: there is no path from a client to a new room.
    internal async Task<CreateRoomOutcome> CreateAsync(RoomId roomId, RoomPlayer[] players)
    {
        var outcome = Register(roomId, players);
        if (outcome != CreateRoomOutcome.Created)
        {
            return outcome;
        }

        // The pairing worker issues both tickets the moment this call returns, so the key the entry point
        // routes on has to exist before it does.
        await locations.StoreAsync(roomId, options.Value.Name);
        return outcome;
    }

    // ReSharper disable once InconsistentlySynchronizedField
    internal bool TryGet(RoomId roomId, out DuelRoom? room) => _rooms.TryGetValue(roomId, out room);

    // The gate cannot be held across an await, so everything that has to happen under it happens here.
    private CreateRoomOutcome Register(RoomId roomId, RoomPlayer[] players)
    {
        lock (_createGate)
        {
            if (_rooms.ContainsKey(roomId))
            {
                return CreateRoomOutcome.AlreadyExists;
            }

            if (_rooms.Count >= options.Value.Capacity)
            {
                return CreateRoomOutcome.AtCapacity;
            }

            var room = new DuelRoom(roomId, players, rules, masterData, logger, matchFinished, Release);
            _rooms[roomId] = room;
            _ = ObserveAsync(room, loopers.RegisterActionAsync((in _) => Tick(room)));
            return CreateRoomOutcome.Created;
        }
    }

    // How much of the tick budget a room spends is what says whether this process can take another one,
    // so every tick is measured. Reading the timestamps allocates nothing and costs less than the tick
    // itself, which is what lets the measurement sit on the loop thread.
    private bool Tick(DuelRoom room)
    {
        var started = Stopwatch.GetTimestamp();
        var running = room.Tick();

        metrics.RecordTick(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return running;
    }

    // A tick that throws reaches no catch anywhere: LogicLooper hands the exception to the registration
    // task and quietly unregisters the action. Unobserved, that is a room that never ticks again but
    // still holds its seats and its players' return tickets.
    internal async Task ObserveAsync(DuelRoom room, Task ticking)
    {
        try
        {
            await ticking;
        }
        catch (Exception exception)
        {
            logger.RoomTickFaulted(exception, room.RoomId);
            room.Abort();
        }
    }

    // Runs on the loop thread as the room shuts down, so the Redis work is left to run on its own: the
    // room stops being routable and both players are free to queue again the moment their entries are gone.
    private void Release(DuelRoom room)
    {
        _rooms.TryRemove(new KeyValuePair<RoomId, DuelRoom>(room.RoomId, room));
        _ = locations.RemoveAsync(room.RoomId);

        foreach (var player in room.Players)
        {
            _ = activeRooms.ReleaseAsync(player.UserId);
        }
    }
}
