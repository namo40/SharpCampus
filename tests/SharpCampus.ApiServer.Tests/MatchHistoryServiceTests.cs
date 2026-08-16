using Grpc.Core;
using NSubstitute;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public sealed class MatchHistoryServiceTests(SupabaseTestFactory factory) : IClassFixture<SupabaseTestFactory>, IDisposable
{
    private const int RecentCount = 20;

    private static readonly DateTimeOffset _played = new(2026, 8, 15, 21, 0, 0, TimeSpan.Zero);
    private static readonly UserId _caller = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId _rival = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private readonly MagicOnionTestClient _client = new();
    private readonly IProfileRepository _profiles = Substitute.For<IProfileRepository>();
    private readonly IMatchHistoryRepository _history = Substitute.For<IMatchHistoryRepository>();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Matches_KeepTheNewestFirstOrderTheStoreAnsweredWith()
    {
        Recorded(
            Row(_played, _rival),
            Row(_played.AddMinutes(-10), _rival),
            Row(_played.AddMinutes(-25), _rival));
        Named((_rival, "bob"));

        var matches = await CreateClient().GetMyMatchesAsync();

        Assert.Equal(
            [_played, _played.AddMinutes(-10), _played.AddMinutes(-25)],
            matches.Select(match => match.PlayedAt));
    }

    [Fact]
    public async Task Matches_AreNeverAskedForBeyondThePageTheServiceServes()
    {
        Recorded();

        await CreateClient().GetMyMatchesAsync();

        await _history.Received(1).GetRecentAsync(_caller, RecentCount);
    }

    [Fact]
    public async Task Opponent_IsNamedFromTheRowOnTheOtherSideOfTheSameGame()
    {
        Recorded(Row(_played, _rival));
        Named((_rival, "bob"));

        var matches = await CreateClient().GetMyMatchesAsync();

        Assert.Equal("bob", Assert.Single(matches).OpponentNickname);

        // One query names the whole page, however many games are on it.
        await _profiles.Received(1).GetNicknamesAsync(Arg.Any<IReadOnlyCollection<UserId>>());
    }

    [Fact]
    public async Task OpponentWithoutAProfileRow_FallsBackToTheNicknameANewProfileWouldGet()
    {
        Recorded(Row(_played, _rival));

        var matches = await CreateClient().GetMyMatchesAsync();

        Assert.Equal(NicknameRules.CreateInitial(_rival), Assert.Single(matches).OpponentNickname);
    }

    [Fact]
    public async Task GameMissingItsOtherRow_IsStillAnsweredFromTheCallersOwnSide()
    {
        Recorded(Row(_played, opponentId: null, ratingAfter: 1042));

        var match = Assert.Single(await CreateClient().GetMyMatchesAsync());

        Assert.Equal(string.Empty, match.OpponentNickname);
        Assert.Equal(new Rating(1042), match.RatingAfter);
    }

    [Fact]
    public async Task PlayerWhoHasNeverFinishedAGame_IsAnsweredWithNoMatchesAtAll()
    {
        Recorded();

        Assert.Empty(await CreateClient().GetMyMatchesAsync());
    }

    [Fact]
    public async Task Reading_LeavesAnAccountThatHasNeverPlayedWithoutAProfileRow()
    {
        Recorded();

        await CreateClient().GetMyMatchesAsync();

        await _profiles.DidNotReceive().CreateIfAbsentAsync(Arg.Any<UserId>(), Arg.Any<string>());
    }

    [Fact]
    public async Task OutcomeAndEndReason_TravelAsTheStringsSettlementWroteDown()
    {
        Recorded(Row(_played, _rival, outcome: "loss", endReason: "disconnect"));
        Named((_rival, "bob"));

        var match = Assert.Single(await CreateClient().GetMyMatchesAsync());

        Assert.Equal("loss", match.Outcome);
        Assert.Equal("disconnect", match.EndReason);
    }

    [Fact]
    public async Task GetMyMatchesAsync_WithoutAToken_IsRejected()
    {
        var client = _client.Create<IMatchHistoryService>(factory);

        var exception = await Assert.ThrowsAsync<RpcException>(async () => await client.GetMyMatchesAsync());

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    private static MatchHistoryRow Row(
        DateTimeOffset playedAt,
        UserId? opponentId,
        string outcome = "win",
        string endReason = "top_out",
        int ratingBefore = 1000,
        int ratingAfter = 1016,
        int coinsAwarded = 50) =>
        new(
            new MatchRecord(new MatchId(Ulid.NewUlid()), _caller, outcome, endReason)
            {
                RatingBefore = new Rating(ratingBefore),
                RatingAfter = new Rating(ratingAfter),
                CoinsAwarded = new Coins(coinsAwarded),
                CreatedAt = playedAt,
            },
            opponentId);

    // Stands up a history holding the games handed over, in the order the store would answer with.
    private void Recorded(params MatchHistoryRow[] rows) =>
        _history.GetRecentAsync(Arg.Any<UserId>(), Arg.Any<int>()).Returns(rows);

    private void Named(params (UserId UserId, string Nickname)[] profiles) =>
        _profiles
            .GetNicknamesAsync(Arg.Any<IReadOnlyCollection<UserId>>())
            .Returns(profiles.ToDictionary(profile => profile.UserId, profile => profile.Nickname));

    private IMatchHistoryService CreateClient() =>
        _client.Create<IMatchHistoryService>(
            factory.WithMatchHistory(_profiles, _history),
            factory.CreateToken(_caller.AsPrimitive(), "player@example.com"));
}
