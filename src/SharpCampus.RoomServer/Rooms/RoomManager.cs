using System.Collections.Concurrent;
using Cysharp.Threading;
using Microsoft.Extensions.Options;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Values;

namespace SharpCampus.RoomServer.Rooms;

public sealed class RoomManager(
    ILogicLooperPool loopers,
    DuelRules rules,
    IActiveRoomStore activeRooms,
    IOptions<RoomServerOptions> options,
    ILogger<RoomManager> logger)
{
    private readonly ConcurrentDictionary<RoomId, DuelRoom> _rooms = new();

    // Creation registers a loop action and has to respect the capacity it just read, so it is serialised;
    // lookups stay lock free.
    private readonly Lock _createGate = new();

    internal int RoomCount => _rooms.Count;

    // Rooms only ever come into being through the ApiServer: there is no path from a client to a new room.
    internal CreateRoomOutcome Create(RoomId roomId, RoomPlayer[] players)
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

            var room = new DuelRoom(roomId, players, rules, logger, Release);
            _rooms[roomId] = room;
            _ = ObserveAsync(room, loopers.RegisterActionAsync((in _) => room.Tick()));
            return CreateRoomOutcome.Created;
        }
    }

    internal bool TryGet(RoomId roomId, out DuelRoom? room) => _rooms.TryGetValue(roomId, out room);

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

    // Runs on the loop thread as the room shuts down, so the Redis work is left to run on its own: both
    // players are free to queue again the moment their entries are gone.
    private void Release(DuelRoom room)
    {
        _rooms.TryRemove(new KeyValuePair<RoomId, DuelRoom>(room.RoomId, room));

        foreach (var player in room.Players)
        {
            _ = activeRooms.ReleaseAsync(player.UserId);
        }
    }
}
