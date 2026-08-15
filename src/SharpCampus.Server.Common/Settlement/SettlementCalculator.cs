using SharpCampus.GameCore;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Settlement;

// What a game is worth, decided from the two profiles and the economy row alone. Kept pure so the
// rules are testable without a database and the write below stays a write.
public static class SettlementCalculator
{
    public const string Win = "win";
    public const string Loss = "loss";
    public const string Draw = "draw";

    private const double RatingScale = 400.0;

    public static MatchSettlement Calculate(
        MatchSettlementRequest request,
        Profile profile1,
        Profile profile2,
        Economy economy)
    {
        var (first, second) = request.Outcome switch
        {
            DuelOutcome.Player1Wins => (Win, Loss),
            DuelOutcome.Player2Wins => (Loss, Win),
            DuelOutcome.Draw => (Draw, Draw),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                request.Outcome,
                "A match that is still running has nothing to settle."),
        };

        return new MatchSettlement(
            Settle(request, request.Player1, profile1, profile2.Rating, first, economy),
            Settle(request, request.Player2, profile2, profile1.Rating, second, economy));
    }

    private static SettledPlayer Settle(
        MatchSettlementRequest request,
        MatchSettlementPlayer player,
        Profile profile,
        Rating opponentRating,
        string outcome,
        Economy economy)
    {
        var rating = NewRating(profile.Rating, opponentRating, outcome, economy.RatingK);

        // A draw pays what a loss pays: neither player earned the win, but both played the game out.
        var awarded = outcome == Win ? economy.WinCoins : economy.LoseCoins;
        var stats = player.Stats;

        return new SettledPlayer(
            player.UserId,
            new Coins(profile.Coins.AsPrimitive() + awarded.AsPrimitive()),
            rating,
            new MatchRecord(request.MatchId, player.UserId, outcome, EndReasonOf(request.Reason))
            {
                RatingBefore = profile.Rating,
                RatingAfter = rating,
                CoinsAwarded = awarded,
                LinesCleared = stats.LinesCleared,
                Quads = stats.Quads,
                GarbageSent = stats.GarbageSent,
                HardDrops = stats.HardDrops,
                MaxCombo = stats.MaxCombo,
                DurationTicks = request.DurationTicks,
            });
    }

    // Elo: R' = R + K(S - E), E = 1 / (1 + 10^((opponent - R) / 400)).
    private static Rating NewRating(Rating rating, Rating opponent, string outcome, int k)
    {
        // A draw moves nothing. Scoring it as the half point Elo asks for would still drag the two
        // ratings towards each other, and a double top-out is not evidence about either player.
        if (outcome == Draw)
        {
            return rating;
        }

        var current = rating.AsPrimitive();
        var expected = 1.0 / (1.0 + Math.Pow(10.0, (opponent.AsPrimitive() - current) / RatingScale));
        var score = outcome == Win ? 1.0 : 0.0;
        var updated = current + (int)Math.Round(k * (score - expected), MidpointRounding.AwayFromZero);

        return new Rating(Math.Max(0, updated));
    }

    private static string EndReasonOf(MatchEndReason reason) => reason switch
    {
        MatchEndReason.TopOut => "top_out",
        MatchEndReason.Forfeit => "forfeit",
        MatchEndReason.Disconnect => "disconnect",
        _ => throw new ArgumentOutOfRangeException(
            nameof(reason),
            reason,
            "A match that never started has nothing to settle."),
    };
}
