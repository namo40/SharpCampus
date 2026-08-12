using MagicOnion.Server.Hubs;
using NSubstitute;
using SharpCampus.GameCore;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Shared.Duel;
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
}

internal static class RoomFixture
{
    public static DuelRules Rules(int inputPerTickMax = 10, int countdownTicks = 2, int joinTimeoutTicks = 5)
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
            joinTimeoutTicks);

    public static IGroup<IDuelHubReceiver> Group(RecordingReceiver receiver)
    {
        var group = Substitute.For<IGroup<IDuelHubReceiver>>();
        group.All.Returns(receiver);
        return group;
    }

    public static JoinRoomResult Join(this DuelRoom room, IGroup<IDuelHubReceiver> group, string displayName)
    {
        var completion = new TaskCompletionSource<JoinRoomResult>();
        Assert.True(room.TryPost(RoomCommand.Join(displayName, group, completion)));
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
}
