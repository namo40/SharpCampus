using Grpc.Core;
using NSubstitute;
using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.Leaderboards;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public sealed class LeaderboardServiceTests(SupabaseTestFactory factory) : IClassFixture<SupabaseTestFactory>, IDisposable
{
    private const int TopCount = 100;
    private const string RatingBoard = "lb:rating";
    private const string DailyBoard = "lb:daily:20260815";

    private static readonly DateTimeOffset _clock = new(2026, 8, 15, 23, 30, 0, TimeSpan.Zero);
    private static readonly UserId _first = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId _second = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    private readonly MagicOnionTestClient _client = new();
    private readonly IProfileRepository _profiles = Substitute.For<IProfileRepository>();
    private readonly ILeaderboardStore _leaderboard = Substitute.For<ILeaderboardStore>();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Top_CountsPlacesFromOneWhereRedisCountsThemFromZero()
    {
        Board([Place(_first, 0, 1500), Place(_second, 1, 1400)]);
        Named((_first, "alice"), (_second, "bob"));

        var view = await CreateClient(Guid.NewGuid()).GetLeaderboardAsync(LeaderboardKind.Rating);

        Assert.Equal([1, 2], view.Top.Select(entry => entry.Rank));
        Assert.Equal(["alice", "bob"], view.Top.Select(entry => entry.DisplayName));
        Assert.Equal([1500, 1400], view.Top.Select(entry => entry.Score));
    }

    [Fact]
    public async Task RatingKind_ReadsTheBoardEverySettlementWritesTo()
    {
        var userId = Guid.NewGuid();
        Board([]);

        await CreateClient(userId).GetLeaderboardAsync(LeaderboardKind.Rating);

        await _leaderboard.Received(1).TopAsync(RatingBoard, TopCount);
        await _leaderboard.Received(1).FindAsync(RatingBoard, new UserId(userId));
    }

    [Fact]
    public async Task DailyWinsKind_ReadsTheBoardKeyedByTheUtcDateOfTheClock()
    {
        var userId = Guid.NewGuid();
        Board([]);

        await CreateClient(userId).GetLeaderboardAsync(LeaderboardKind.DailyWins);

        await _leaderboard.Received(1).TopAsync(DailyBoard, TopCount);
        await _leaderboard.Received(1).FindAsync(DailyBoard, new UserId(userId));
    }

    [Fact]
    public async Task Me_IsAnsweredWithTheGlobalPlaceEvenWhenItRunsPastTheListedOnes()
    {
        var userId = Guid.NewGuid();
        Board([Place(_first, 0, 1500)], me: Place(new UserId(userId), 412, 900));
        Named((_first, "alice"), (new UserId(userId), "straggler"));

        var view = await CreateClient(userId).GetLeaderboardAsync(LeaderboardKind.Rating);

        Assert.Equal(413, view.Me?.Rank);
        Assert.Equal("straggler", view.Me?.DisplayName);
        Assert.Equal(900, view.Me?.Score);
    }

    [Fact]
    public async Task Me_IsFilledEvenWhenTheCallerIsOneOfTheListedPlaces()
    {
        var userId = Guid.NewGuid();
        var caller = new UserId(userId);
        Board([Place(_first, 0, 1500), Place(caller, 1, 1400)], me: Place(caller, 1, 1400));
        Named((_first, "alice"), (caller, "bob"));

        var view = await CreateClient(userId).GetLeaderboardAsync(LeaderboardKind.Rating);

        Assert.Equal(2, view.Me?.Rank);
        Assert.Equal(2, view.Top.Length);

        // The whole page is one profile query, and the caller's own row is not asked for twice.
        await _profiles.Received(1).GetNicknamesAsync(Arg.Is<IReadOnlyCollection<UserId>>(ids => ids.Count == 2));
    }

    [Fact]
    public async Task Me_IsNullForACallerTheBoardHasNeverHeardOf()
    {
        Board([Place(_first, 0, 1500)]);
        Named((_first, "alice"));

        var view = await CreateClient(Guid.NewGuid()).GetLeaderboardAsync(LeaderboardKind.Rating);

        Assert.Null(view.Me);
    }

    [Fact]
    public async Task PlaceWithoutAProfileRow_FallsBackToTheNicknameANewProfileWouldGet()
    {
        Board([Place(_first, 0, 1500), Place(_second, 1, 1400)]);
        Named((_first, "alice"));

        var view = await CreateClient(Guid.NewGuid()).GetLeaderboardAsync(LeaderboardKind.Rating);

        Assert.Equal("alice", view.Top[0].DisplayName);
        Assert.Equal(NicknameRules.CreateInitial(_second), view.Top[1].DisplayName);
    }

    [Fact]
    public async Task BoardNothingHasSettledOnto_IsAnsweredWithNoPlacesAndNoOwnPlace()
    {
        Board([]);

        var view = await CreateClient(Guid.NewGuid()).GetLeaderboardAsync(LeaderboardKind.Rating);

        Assert.Empty(view.Top);
        Assert.Null(view.Me);
    }

    [Fact]
    public async Task GetLeaderboardAsync_WithoutAToken_IsRejected()
    {
        var client = _client.Create<ILeaderboardService>(factory);

        var exception = await Assert.ThrowsAsync<RpcException>(
            async () => await client.GetLeaderboardAsync(LeaderboardKind.Rating));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    private static LeaderboardRow Place(UserId userId, long rank, int score) => new(userId, rank, score);

    // Stands up a board holding the places handed over, and whatever it answers about the caller.
    private void Board(LeaderboardRow[] top, LeaderboardRow? me = null)
    {
        _leaderboard.TopAsync(Arg.Any<string>(), Arg.Any<int>()).Returns(top);
        _leaderboard.FindAsync(Arg.Any<string>(), Arg.Any<UserId>()).Returns(me);
    }

    private void Named(params (UserId UserId, string Nickname)[] profiles) =>
        _profiles
            .GetNicknamesAsync(Arg.Any<IReadOnlyCollection<UserId>>())
            .Returns(profiles.ToDictionary(profile => profile.UserId, profile => profile.Nickname));

    private ILeaderboardService CreateClient(Guid userId) =>
        _client.Create<ILeaderboardService>(
            factory.WithLeaderboards(_profiles, _leaderboard, new FixedTimeProvider(_clock)),
            factory.CreateToken(userId, "player@example.com"));

    // A clock the test pins, so which day's board a call asks for is the test's choice and not the machine's.
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
