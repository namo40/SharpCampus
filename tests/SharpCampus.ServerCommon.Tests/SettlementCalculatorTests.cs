using SharpCampus.GameCore;
using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.Settlement;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class SettlementCalculatorTests
{
    private static readonly UserId _first = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId _second = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private static readonly Economy _economy = new()
    {
        Id = GameConfig.SingleRowId,
        WinCoins = new Coins(100),
        LoseCoins = new Coins(30),
        RatingK = 32,
        RatingInitial = new Rating(1000),
    };

    [Fact]
    public void EvenlyRatedPlayers_TradeHalfTheKFactor()
    {
        var settlement = SettlementCalculator.Calculate(
            Request(DuelOutcome.Player1Wins),
            ProfileAt(_first, rating: 1000, coins: 0),
            ProfileAt(_second, rating: 1000, coins: 0),
            _economy);

        Assert.Equal(new Rating(1016), settlement.Player1.Rating);
        Assert.Equal(new Rating(984), settlement.Player2.Rating);
    }

    [Fact]
    public void BeatingAWeakerOpponent_IsWorthLessThanBeatingAnEqualOne()
    {
        var settlement = SettlementCalculator.Calculate(
            Request(DuelOutcome.Player1Wins),
            ProfileAt(_first, rating: 1200, coins: 0),
            ProfileAt(_second, rating: 1000, coins: 0),
            _economy);

        Assert.Equal(new Rating(1208), settlement.Player1.Rating);
        Assert.Equal(new Rating(992), settlement.Player2.Rating);
    }

    [Fact]
    public void RatingNeverFallsBelowZero()
    {
        var settlement = SettlementCalculator.Calculate(
            Request(DuelOutcome.Player2Wins),
            ProfileAt(_first, rating: 10, coins: 0),
            ProfileAt(_second, rating: 10, coins: 0),
            _economy);

        Assert.Equal(new Rating(0), settlement.Player1.Rating);
        Assert.Equal(new Rating(26), settlement.Player2.Rating);
    }

    [Fact]
    public void ADraw_MovesNoRatingAndPaysBothSidesTheLosingShare()
    {
        var settlement = SettlementCalculator.Calculate(
            Request(DuelOutcome.Draw),
            ProfileAt(_first, rating: 1200, coins: 500),
            ProfileAt(_second, rating: 900, coins: 40),
            _economy);

        Assert.Equal(new Rating(1200), settlement.Player1.Rating);
        Assert.Equal(new Rating(900), settlement.Player2.Rating);
        Assert.Equal(new Coins(530), settlement.Player1.Coins);
        Assert.Equal(new Coins(70), settlement.Player2.Coins);
        Assert.Equal(new Coins(30), settlement.Player1.Record.CoinsAwarded);
        Assert.Equal("draw", settlement.Player1.Record.Outcome);
        Assert.Equal("draw", settlement.Player2.Record.Outcome);
    }

    [Fact]
    public void CoinsArePaidOnTopOfWhatTheAccountAlreadyHas()
    {
        var settlement = SettlementCalculator.Calculate(
            Request(DuelOutcome.Player1Wins),
            ProfileAt(_first, rating: 1000, coins: 250),
            ProfileAt(_second, rating: 1000, coins: 250),
            _economy);

        Assert.Equal(new Coins(350), settlement.Player1.Coins);
        Assert.Equal(new Coins(280), settlement.Player2.Coins);
        Assert.Equal(new Coins(100), settlement.Player1.Record.CoinsAwarded);
        Assert.Equal(new Coins(30), settlement.Player2.Record.CoinsAwarded);
    }

    [Theory]
    [InlineData(MatchEndReason.TopOut, "top_out")]
    [InlineData(MatchEndReason.Forfeit, "forfeit")]
    [InlineData(MatchEndReason.Disconnect, "disconnect")]
    public void AGivenUpOrDroppedMatch_SettlesAsAnyOtherLoss(MatchEndReason reason, string expected)
    {
        var settlement = SettlementCalculator.Calculate(
            Request(DuelOutcome.Player1Wins, reason),
            ProfileAt(_first, rating: 1000, coins: 0),
            ProfileAt(_second, rating: 1000, coins: 0),
            _economy);

        Assert.Equal(expected, settlement.Player2.Record.EndReason);
        Assert.Equal("loss", settlement.Player2.Record.Outcome);
        Assert.Equal(new Rating(984), settlement.Player2.Rating);
    }

    [Fact]
    public void ARecordRowCarriesTheMatchAndTheBoardThatPlayedIt()
    {
        var request = Request(DuelOutcome.Player1Wins);

        var settlement = SettlementCalculator.Calculate(
            request,
            ProfileAt(_first, rating: 1000, coins: 0),
            ProfileAt(_second, rating: 1000, coins: 0),
            _economy);

        var record = settlement.Player1.Record;

        Assert.Equal(request.MatchId, record.MatchId);
        Assert.Equal(_first, record.UserId);
        Assert.Equal("win", record.Outcome);
        Assert.Equal(new Rating(1000), record.RatingBefore);
        Assert.Equal(new Rating(1016), record.RatingAfter);
        Assert.Equal(640, record.DurationTicks);
        Assert.Equal(12, record.LinesCleared);
        Assert.Equal(2, record.Quads);
        Assert.Equal(7, record.GarbageSent);
        Assert.Equal(30, record.HardDrops);
        Assert.Equal(4, record.MaxCombo);

        // Left at its default so the database stamps it, exactly as a new profile is stamped.
        Assert.Equal(default, record.CreatedAt);
    }

    [Fact]
    public void AMatchThatIsStillRunning_HasNothingToSettle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SettlementCalculator.Calculate(
            Request(DuelOutcome.Ongoing),
            ProfileAt(_first, rating: 1000, coins: 0),
            ProfileAt(_second, rating: 1000, coins: 0),
            _economy));
    }

    [Fact]
    public void AMatchThatNeverStarted_HasNothingToSettle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SettlementCalculator.Calculate(
            Request(DuelOutcome.Draw, MatchEndReason.Aborted),
            ProfileAt(_first, rating: 1000, coins: 0),
            ProfileAt(_second, rating: 1000, coins: 0),
            _economy));
    }

    private static MatchSettlementRequest Request(
        DuelOutcome outcome,
        MatchEndReason reason = MatchEndReason.TopOut) => new(
        MatchId.New(),
        outcome,
        reason,
        DurationTicks: 640,
        new MatchSettlementPlayer(_first, new BoardStats(12, 2, 7, 30, 4)),
        new MatchSettlementPlayer(_second, new BoardStats(5, 0, 1, 18, 2)));

    private static Profile ProfileAt(UserId userId, int rating, int coins) =>
        new(userId, "player") { Rating = new Rating(rating), Coins = new Coins(coins) };
}
