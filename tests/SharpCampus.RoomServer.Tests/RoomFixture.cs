using MagicOnion.Server;
using MagicOnion.Server.Hubs;
using MessagePipe;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharpCampus.GameCore;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Matchmaking;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

// Records what a room broadcasts, standing in for the two connected clients.
internal sealed class RecordingReceiver : IDuelHubReceiver
{
    private readonly TaskCompletionSource<MatchStartInfo> _starting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<MatchResult> _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<MatchStartInfo> Started { get; } = [];

    public List<TickDelta> Deltas { get; } = [];

    public List<MatchResult> Finished { get; } = [];

    public List<(int PlayerIndex, bool Connected)> Presence { get; } = [];

    public List<int> RematchAsks { get; } = [];

    public int RematchDeclines { get; private set; }

    // Never completes unless a test hands the room an answer, which is what the offer's own timeout
    // needs to be reachable.
    public Task<bool> RematchReply { get; set; } = new TaskCompletionSource<bool>().Task;

    public Task<MatchStartInfo> Starting => _starting.Task;

    public Task<MatchResult> Ended => _ended.Task;

    public void OnMatchStarting(MatchStartInfo info)
    {
        Started.Add(info);
        _starting.TrySetResult(info);
    }

    public void OnTickDelta(TickDelta delta) => Deltas.Add(delta);

    public void OnMatchFinished(MatchResult result)
    {
        Finished.Add(result);
        _ended.TrySetResult(result);
    }

    public void OnPresenceChanged(PlayerIndex player, bool connected)
        => Presence.Add((player.AsPrimitive(), connected));

    public Task<bool> AskRematchAsync(int timeoutSeconds, CancellationToken cancellationToken)
    {
        RematchAsks.Add(timeoutSeconds);
        return RematchReply.WaitAsync(cancellationToken);
    }

    public void OnRematchDeclined() => RematchDeclines++;
}

// The room reaches one client through Single and both through All, so the double has to tell the two
// apart: the rematch offer is addressed to a connection, not to the group.
internal sealed class FakeGroup(RecordingReceiver all) : IGroup<IDuelHubReceiver>
{
    private readonly RecordingReceiver _all = all;
    private readonly Dictionary<Guid, RecordingReceiver> _connections = [];
    private bool _disposed;

    public IDuelHubReceiver All
    {
        get
        {
            ThrowIfDisposed();
            return _all;
        }
    }

    public RecordingReceiver Connection(Guid connectionId)
    {
        if (!_connections.TryGetValue(connectionId, out var receiver))
        {
            receiver = new RecordingReceiver();
            _connections[connectionId] = receiver;
        }

        return receiver;
    }

    public IDuelHubReceiver Except(IEnumerable<Guid> excludes) => All;

    public IDuelHubReceiver Only(IEnumerable<Guid> targets) => All;

    public IDuelHubReceiver Single(Guid target)
    {
        ThrowIfDisposed();
        return Connection(target);
    }

    // Multicaster disposes a group with the last connection that leaves it, and every later touch
    // throws. Tests flip this to stage that moment.
    public void Dispose() => _disposed = true;

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public ValueTask RemoveAsync(ServiceContext context) => default;

    public ValueTask<int> CountAsync() => new(_connections.Count);
}

// A room publishes without waiting for anybody, so keeping what went past is the whole of it.
internal sealed class RecordingPublisher : IAsyncPublisher<MatchFinishedEvent>
{
    public List<MatchFinishedEvent> Published { get; } = [];

    public void Publish(MatchFinishedEvent message, CancellationToken cancellationToken = default)
        => Published.Add(message);

    public ValueTask PublishAsync(MatchFinishedEvent message, CancellationToken cancellationToken = default)
    {
        Publish(message, cancellationToken);
        return default;
    }

    public ValueTask PublishAsync(
        MatchFinishedEvent message,
        AsyncPublishStrategy publishStrategy,
        CancellationToken cancellationToken = default)
        => PublishAsync(message, cancellationToken);
}

internal static class RoomFixture
{
    public static readonly UserId FirstUser = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly UserId SecondUser = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    public static readonly SkinId FreeSkin = new("CLASSIC");
    public static readonly SkinId PaidSkin = new("MONO");

    public static readonly Guid[] Connections =
    [
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
    ];

    public static RoomPlayer[] Players() =>
        [new RoomPlayer(FirstUser, "one", PaidSkin), new RoomPlayer(SecondUser, "two", FreeSkin)];

    // Only the skin table: the tick rules a room runs on come from Rules() rather than from master data.
    public static MemoryDatabase MasterData()
    {
        var builder = new DatabaseBuilder();
        builder.Append([NewSkin(FreeSkin, 0), NewSkin(PaidSkin, 200)]);

        return new MemoryDatabase(builder.Build());
    }

    public static IOptions<RoomServerOptions> Options(int capacity = 100) =>
        Microsoft.Extensions.Options.Options.Create(new RoomServerOptions
        {
            Name = "test",
            ClientEndpoint = "http://localhost:5002",
            ControlEndpoint = "http://localhost:5002",
            Capacity = capacity,
        });

    public static DuelRules Rules(
        int inputPerTickMax = 10,
        int countdownTicks = 2,
        int joinTimeoutTicks = 5,
        int graceTicks = 4,
        int rematchTimeoutTicks = 60)
        => new(
            new SimulationConfig(
                LockDelayTicks: 10,
                LockResetMax: 15,
                NextCount: 5,
                GarbageCapPerLock: 8,
                GravityTicksPerCell: [100_000],
                LevelUpIntervalTicks: 600,
                MaxLevel: 1,
                AttackByLines: [0, 1, 2, 4],
                ComboBonus: [new ComboBonusRange(2, 3, 1)]),
            TickRate: 20,
            inputPerTickMax,
            NextCount: 5,
            countdownTicks,
            joinTimeoutTicks,
            graceTicks,
            rematchTimeoutTicks);

    public static IActiveRoomStore ActiveRooms() => Substitute.For<IActiveRoomStore>();

    public static RecordingPublisher Publisher() => new();

    public static FakeGroup Group(RecordingReceiver receiver) => new(receiver);

    public static JoinRoomResult Join(this DuelRoom room, FakeGroup group, UserId userId, Guid connectionId = default)
    {
        var completion = new TaskCompletionSource<JoinRoomResult>();
        Assert.True(room.TryPost(RoomCommand.Join(userId, connectionId, group, completion)));
        room.Tick();

        Assert.True(completion.Task.IsCompleted);
        return completion.Task.Result;
    }

    // Ticks out the countdown, so a test can go straight to what it is about.
    public static DuelRoom TickToPlaying(this DuelRoom room)
    {
        for (var i = 0; i < 100 && room.State != RoomState.Playing; i++)
        {
            room.Tick();
        }

        Assert.Equal(RoomState.Playing, room.State);
        return room;
    }

    private static Skin NewSkin(SkinId skinId, int price) => new()
    {
        SkinId = skinId,
        NameKey = $"skin.{skinId.AsPrimitive().ToLowerInvariant()}.name",
        Price = new Coins(price),
        BlockGlyph = "[]",
        ColorI = "Cyan",
        ColorO = "Yellow",
        ColorT = "Magenta",
        ColorS = "Green",
        ColorZ = "Red",
        ColorJ = "Blue",
        ColorL = "DarkYellow",
        ColorGarbage = "DarkGray",
    };
}
