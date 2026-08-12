using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Cysharp.Threading;
using Microsoft.Extensions.Options;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Values;

namespace SharpCampus.RoomServer.Rooms;

public sealed class RoomManager(
    ILogicLooperPool loopers,
    DuelRules rules,
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

            var room = new DuelRoom(roomId, players, rules, NewSeed(), logger, Remove);
            _rooms[roomId] = room;
            _ = ObserveAsync(room, loopers.RegisterActionAsync((in _) => room.Tick()));
            return CreateRoomOutcome.Created;
        }
    }

    internal bool TryGet(RoomId roomId, out DuelRoom? room) => _rooms.TryGetValue(roomId, out room);

    // Seeds are per room and server-side: both boards deal from it, so a client that knew it could
    // read the opponent's piece order.
    private static ulong NewSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        RandomNumberGenerator.Fill(bytes);
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    // A tick that throws reaches no catch anywhere: LogicLooper hands the exception to the registration
    // task and quietly unregisters the action. Unobserved, that is a room that never ticks again but
    // still sits in the table as if it were running.
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

    private void Remove(DuelRoom room)
        => _rooms.TryRemove(new KeyValuePair<RoomId, DuelRoom>(room.RoomId, room));
}
