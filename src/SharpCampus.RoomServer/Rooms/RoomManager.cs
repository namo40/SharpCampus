using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Cysharp.Threading;
using SharpCampus.RoomServer.MasterData;

namespace SharpCampus.RoomServer.Rooms;

public sealed class RoomManager(ILogicLooperPool loopers, DuelRules rules, ILogger<RoomManager> logger)
{
    private readonly ConcurrentDictionary<string, DuelRoom> _rooms = new(StringComparer.Ordinal);

    // Creation registers a loop action, which must happen once per room; lookups stay lock free.
    private readonly Lock _createGate = new();

    internal int RoomCount => _rooms.Count;

    internal DuelRoom GetOrCreate(string roomKey)
    {
        if (_rooms.TryGetValue(roomKey, out var existing) && !existing.IsClosed)
        {
            return existing;
        }

        lock (_createGate)
        {
            if (_rooms.TryGetValue(roomKey, out existing) && !existing.IsClosed)
            {
                return existing;
            }

            var room = new DuelRoom(roomKey, rules, NewSeed(), logger, Remove);
            _rooms[roomKey] = room;
            _ = ObserveAsync(room, loopers.RegisterActionAsync((in _) => room.Tick()));
            return room;
        }
    }

    internal bool TryGet(string roomKey, out DuelRoom? room) => _rooms.TryGetValue(roomKey, out room);

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
            logger.RoomTickFaulted(exception, room.RoomKey);
            room.Abort();
        }
    }

    private void Remove(DuelRoom room)
        => _rooms.TryRemove(new KeyValuePair<string, DuelRoom>(room.RoomKey, room));
}
