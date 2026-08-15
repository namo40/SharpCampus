using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpCampus.GameCore;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Missions;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public sealed class MissionProgressHandlerTests
{
    private static readonly DateTimeOffset _clock = new(2026, 8, 15, 23, 30, 0, TimeSpan.Zero);

    private readonly IMissionRepository _missions = Substitute.For<IMissionRepository>();
    private readonly List<Recorded> _recorded = [];

    public MissionProgressHandlerTests() =>
        _missions
            .When(m => m.RecordAsync(
                Arg.Any<UserId>(),
                Arg.Any<DateOnly>(),
                Arg.Any<IReadOnlyList<MissionDelta>>()))
            .Do(call => _recorded.Add(new Recorded(
                call.Arg<UserId>(),
                call.Arg<DateOnly>(),
                call.Arg<IReadOnlyList<MissionDelta>>())));

    [Fact]
    public async Task AFinishedMatch_CountsForBothPlayersButTheWinOnlyForTheWinner()
    {
        await CreateHandler(AllMissions()).HandleAsync(
            Finished(DuelOutcome.Player2Wins),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, Amount(For(RoomFixture.FirstUser), "PLAY_3"));
        Assert.Equal(0, Amount(For(RoomFixture.FirstUser), "WIN_1"));
        Assert.Equal(1, Amount(For(RoomFixture.SecondUser), "PLAY_3"));
        Assert.Equal(1, Amount(For(RoomFixture.SecondUser), "WIN_1"));
    }

    [Fact]
    public async Task ADraw_CountsTheMatchForBothAndTheWinForNeither()
    {
        await CreateHandler(AllMissions()).HandleAsync(
            Finished(DuelOutcome.Draw),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, Amount(For(RoomFixture.FirstUser), "PLAY_3"));
        Assert.Equal(1, Amount(For(RoomFixture.SecondUser), "PLAY_3"));
        Assert.Equal(0, Amount(For(RoomFixture.FirstUser), "WIN_1"));
        Assert.Equal(0, Amount(For(RoomFixture.SecondUser), "WIN_1"));
    }

    [Fact]
    public async Task TheBoardsOwnStats_BecomeTheProgressOfTheMissionsThatCountThem()
    {
        await CreateHandler(AllMissions()).HandleAsync(
            Finished(DuelOutcome.Player2Wins),
            TestContext.Current.CancellationToken);

        var deltas = For(RoomFixture.FirstUser);

        Assert.Equal(12, Amount(deltas, "CLEAR_40"));
        Assert.Equal(7, Amount(deltas, "SEND_20"));
        Assert.Equal(30, Amount(deltas, "HARDDROP_30"));
    }

    [Fact]
    public async Task AMetricThatNeverMoved_IsLeftOutOfTheDeltasEntirely()
    {
        var quiet = new MatchParticipant(RoomFixture.SecondUser, new BoardStats(0, 0, 0, 0, 0));

        await CreateHandler(AllMissions()).HandleAsync(
            Finished(DuelOutcome.Player1Wins, second: quiet),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [new MissionId("PLAY_3")],
            For(RoomFixture.SecondUser).Select(delta => delta.MissionId));
    }

    [Fact]
    public async Task APlayerWithNothingToShowForTheMatch_IsNotWrittenAtAll()
    {
        var quiet = new MatchParticipant(RoomFixture.SecondUser, new BoardStats(0, 0, 0, 0, 0));

        // No mission counts a match here, so the loser of a quiet game moved nothing at all.
        await CreateHandler(MasterData(NewMission("CLEAR_40", MissionMetric.LinesCleared))).HandleAsync(
            Finished(DuelOutcome.Player1Wins, second: quiet),
            TestContext.Current.CancellationToken);

        Assert.Equal([RoomFixture.FirstUser], _recorded.Select(record => record.UserId));
    }

    [Fact]
    public async Task TheDayProgressCountsTowards_IsTheUtcDateOfTheClock()
    {
        await CreateHandler(AllMissions()).HandleAsync(
            Finished(DuelOutcome.Player1Wins),
            TestContext.Current.CancellationToken);

        Assert.All(_recorded, record => Assert.Equal(new DateOnly(2026, 8, 15), record.Date));
    }

    [Fact]
    public async Task AStoreThatFails_IsLoggedRatherThanThrownBackAtThePublisher()
    {
        _missions
            .When(m => m.RecordAsync(
                Arg.Any<UserId>(),
                Arg.Any<DateOnly>(),
                Arg.Any<IReadOnlyList<MissionDelta>>()))
            .Do(_ => throw new InvalidOperationException("no database"));

        await CreateHandler(AllMissions()).HandleAsync(
            Finished(DuelOutcome.Player1Wins),
            TestContext.Current.CancellationToken);
    }

    // The room publishes to both subscribers at once, so the server has to be able to build this one too.
    [Fact]
    public void TheServerCanBuildTheMissionSideOfThePublication()
    {
        using var factory = new RoomServerTestFactory();

        Assert.NotNull(factory.Services.GetRequiredService<MissionProgressHandler>());

        using var scope = factory.Services.CreateScope();
        Assert.IsType<MissionRepository>(scope.ServiceProvider.GetRequiredService<IMissionRepository>());
    }

    private static int Amount(IReadOnlyList<MissionDelta> deltas, string missionId) =>
        deltas.SingleOrDefault(delta => delta.MissionId == new MissionId(missionId)).Amount;

    private static MemoryDatabase AllMissions() => MasterData(
        NewMission("PLAY_3", MissionMetric.MatchesPlayed),
        NewMission("WIN_1", MissionMetric.Wins),
        NewMission("CLEAR_40", MissionMetric.LinesCleared),
        NewMission("SEND_20", MissionMetric.GarbageSent),
        NewMission("HARDDROP_30", MissionMetric.HardDrops));

    private static MemoryDatabase MasterData(params Mission[] missions)
    {
        var builder = new DatabaseBuilder();
        builder.Append(missions);

        return new MemoryDatabase(builder.Build());
    }

    private static Mission NewMission(string missionId, MissionMetric metric) => new()
    {
        MissionId = new MissionId(missionId),
        NameKey = $"mission.{missionId.ToLowerInvariant()}.name",
        Metric = metric,
        Goal = 3,
        RewardCoins = new Coins(30),
    };

    private static MatchFinishedEvent Finished(
        DuelOutcome outcome,
        MatchParticipant? first = null,
        MatchParticipant? second = null) => new(
        MatchId.New(),
        new RoomId(Ulid.NewUlid()),
        outcome,
        MatchEndReason.TopOut,
        DurationTicks: 640,
        first ?? new MatchParticipant(RoomFixture.FirstUser, new BoardStats(12, 2, 7, 30, 4)),
        second ?? new MatchParticipant(RoomFixture.SecondUser, new BoardStats(5, 0, 1, 18, 2)));

    private IReadOnlyList<MissionDelta> For(UserId userId) =>
        Assert.Single(_recorded, record => record.UserId == userId).Deltas;

    private MissionProgressHandler CreateHandler(MemoryDatabase masterData)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _missions);

        // The handler is a singleton and the repository is scoped, which is the whole reason it holds a
        // scope factory rather than the repository itself.
        var provider = services.BuildServiceProvider();

        return new MissionProgressHandler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            masterData,
            new FixedTimeProvider(_clock),
            NullLogger<MissionProgressHandler>.Instance);
    }

    private sealed record Recorded(UserId UserId, DateOnly Date, IReadOnlyList<MissionDelta> Deltas);

    // A clock the test pins, so which day progress lands on is the test's choice and not the machine's.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
