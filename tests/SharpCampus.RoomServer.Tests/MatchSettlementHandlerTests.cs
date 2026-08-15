using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpCampus.GameCore;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.Leaderboards;
using SharpCampus.Server.Common.Settlement;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public sealed class MatchSettlementHandlerTests
{
    private static readonly DateTimeOffset _clock = new(2026, 8, 15, 23, 30, 0, TimeSpan.Zero);
    private static readonly Rating _winnerRating = new(1120);
    private static readonly Rating _loserRating = new(980);

    private readonly IMatchSettlementService _settlement = Substitute.For<IMatchSettlementService>();
    private readonly ILeaderboardStore _leaderboard = Substitute.For<ILeaderboardStore>();

    [Fact]
    public async Task PublishedMatch_ReachesTheSettlementWithItsStatsIntact()
    {
        Settles(SettlementCalculator.Loss, SettlementCalculator.Win);
        var message = Finished();

        await CreateHandler().HandleAsync(message, TestContext.Current.CancellationToken);

        await _settlement.Received(1).SettleAsync(Arg.Is<MatchSettlementRequest>(request =>
            request.MatchId == message.MatchId
            && request.Outcome == DuelOutcome.Player2Wins
            && request.Reason == MatchEndReason.Disconnect
            && request.DurationTicks == 640
            && request.Player1.UserId == RoomFixture.FirstUser
            && request.Player1.Stats.MaxCombo == 4
            && request.Player2.UserId == RoomFixture.SecondUser
            && request.Player2.Stats.HardDrops == 18));
    }

    [Fact]
    public async Task SettledMatch_PutsBothRatingsOnTheBoard()
    {
        Settles(SettlementCalculator.Loss, SettlementCalculator.Win);

        await CreateHandler().HandleAsync(Finished(), TestContext.Current.CancellationToken);

        await _leaderboard.Received(1).SetRatingAsync(RoomFixture.FirstUser, _loserRating);
        await _leaderboard.Received(1).SetRatingAsync(RoomFixture.SecondUser, _winnerRating);
    }

    [Fact]
    public async Task SettledMatch_CountsTheDaysWinForTheWinnerAlone()
    {
        Settles(SettlementCalculator.Loss, SettlementCalculator.Win);

        await CreateHandler().HandleAsync(Finished(), TestContext.Current.CancellationToken);

        await _leaderboard.Received(1).AddDailyWinAsync(RoomFixture.SecondUser, new DateOnly(2026, 8, 15));
        await _leaderboard.DidNotReceive().AddDailyWinAsync(RoomFixture.FirstUser, Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task ADraw_CountsTheDaysWinForNeitherButStillMovesBothRatings()
    {
        Settles(SettlementCalculator.Draw, SettlementCalculator.Draw);

        await CreateHandler().HandleAsync(
            Finished(DuelOutcome.Draw),
            TestContext.Current.CancellationToken);

        await _leaderboard.DidNotReceiveWithAnyArgs().AddDailyWinAsync(default, default);
        await _leaderboard.Received(2).SetRatingAsync(Arg.Any<UserId>(), Arg.Any<Rating>());
    }

    [Fact]
    public async Task SettlementThatFails_IsLoggedRatherThanThrownBackAtThePublisher()
    {
        _settlement
            .When(s => s.SettleAsync(Arg.Any<MatchSettlementRequest>()))
            .Do(_ => throw new InvalidOperationException("no database"));

        await CreateHandler().HandleAsync(Finished(), TestContext.Current.CancellationToken);

        // A board built from a settlement that never happened would be a board nothing can rebuild.
        await _leaderboard.DidNotReceiveWithAnyArgs().SetRatingAsync(default, default);
        await _leaderboard.DidNotReceiveWithAnyArgs().AddDailyWinAsync(default, default);
    }

    [Fact]
    public async Task BoardThatFails_LeavesTheSettlementItself_Untouched()
    {
        Settles(SettlementCalculator.Loss, SettlementCalculator.Win);
        _leaderboard
            .When(l => l.SetRatingAsync(Arg.Any<UserId>(), Arg.Any<Rating>()))
            .Do(_ => throw new InvalidOperationException("no redis"));

        await CreateHandler().HandleAsync(Finished(), TestContext.Current.CancellationToken);

        await _settlement.Received(1).SettleAsync(Arg.Any<MatchSettlementRequest>());
    }

    // A settlement that cannot be built is a settlement that only fails once a match has been played,
    // so the server is asked for every part of it up front.
    [Fact]
    public void TheServerCanBuildBothEndsOfThePublication()
    {
        using var factory = new RoomServerTestFactory();

        Assert.NotNull(factory.Services.GetRequiredService<IAsyncPublisher<MatchFinishedEvent>>());
        Assert.NotNull(factory.Services.GetRequiredService<IAsyncSubscriber<MatchFinishedEvent>>());
        Assert.NotNull(factory.Services.GetRequiredService<MatchSettlementHandler>());
        Assert.IsType<RedisLeaderboardStore>(factory.Services.GetRequiredService<ILeaderboardStore>());

        using var scope = factory.Services.CreateScope();
        Assert.IsType<MatchSettlementService>(scope.ServiceProvider.GetRequiredService<IMatchSettlementService>());
    }

    private static MatchFinishedEvent Finished(DuelOutcome outcome = DuelOutcome.Player2Wins) => new(
        MatchId.New(),
        new RoomId(Ulid.NewUlid()),
        outcome,
        MatchEndReason.Disconnect,
        DurationTicks: 640,
        new MatchParticipant(RoomFixture.FirstUser, new BoardStats(12, 2, 7, 30, 4)),
        new MatchParticipant(RoomFixture.SecondUser, new BoardStats(5, 0, 1, 18, 2)));

    private static SettledPlayer Settled(UserId userId, string outcome) => new(
        userId,
        new Coins(100),
        outcome == SettlementCalculator.Win ? _winnerRating : _loserRating,
        new MatchRecord(MatchId.New(), userId, outcome, "top_out"));

    private void Settles(string first, string second) =>
        _settlement.SettleAsync(Arg.Any<MatchSettlementRequest>()).Returns(new MatchSettlement(
            Settled(RoomFixture.FirstUser, first),
            Settled(RoomFixture.SecondUser, second)));

    private MatchSettlementHandler CreateHandler()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _settlement);

        // The handler is a singleton and the settlement is scoped, which is the whole reason it holds a
        // scope factory rather than the service itself.
        var provider = services.BuildServiceProvider();

        return new MatchSettlementHandler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            _leaderboard,
            new FixedTimeProvider(_clock),
            NullLogger<MatchSettlementHandler>.Instance);
    }

    // A clock the test pins, so which day a win counts towards is the test's choice and not the machine's.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
