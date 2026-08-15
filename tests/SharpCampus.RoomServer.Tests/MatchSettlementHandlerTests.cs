using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpCampus.GameCore;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common.Settlement;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.RoomServer.Tests;

public sealed class MatchSettlementHandlerTests
{
    [Fact]
    public async Task PublishedMatch_ReachesTheSettlementWithItsStatsIntact()
    {
        var settlement = Substitute.For<IMatchSettlementService>();
        var handler = CreateHandler(settlement);
        var message = Finished();

        await handler.HandleAsync(message, TestContext.Current.CancellationToken);

        await settlement.Received(1).SettleAsync(Arg.Is<MatchSettlementRequest>(request =>
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
    public async Task SettlementThatFails_IsLoggedRatherThanThrownBackAtThePublisher()
    {
        var settlement = Substitute.For<IMatchSettlementService>();
        settlement
            .When(s => s.SettleAsync(Arg.Any<MatchSettlementRequest>()))
            .Do(_ => throw new InvalidOperationException("no database"));

        await CreateHandler(settlement).HandleAsync(Finished(), TestContext.Current.CancellationToken);
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

        using var scope = factory.Services.CreateScope();
        Assert.IsType<MatchSettlementService>(scope.ServiceProvider.GetRequiredService<IMatchSettlementService>());
    }

    private static MatchSettlementHandler CreateHandler(IMatchSettlementService settlement)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => settlement);

        // The handler is a singleton and the settlement is scoped, which is the whole reason it holds a
        // scope factory rather than the service itself.
        var provider = services.BuildServiceProvider();

        return new MatchSettlementHandler(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MatchSettlementHandler>.Instance);
    }

    private static MatchFinishedEvent Finished() => new(
        MatchId.New(),
        new RoomId(Ulid.NewUlid()),
        DuelOutcome.Player2Wins,
        MatchEndReason.Disconnect,
        DurationTicks: 640,
        new MatchParticipant(RoomFixture.FirstUser, new BoardStats(12, 2, 7, 30, 4)),
        new MatchParticipant(RoomFixture.SecondUser, new BoardStats(5, 0, 1, 18, 2)));
}
