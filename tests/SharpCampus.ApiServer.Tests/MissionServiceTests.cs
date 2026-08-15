using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Services;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public sealed class MissionServiceTests(SupabaseTestFactory factory) : IClassFixture<SupabaseTestFactory>, IDisposable
{
    private static readonly MissionId _play = new("PLAY_3");
    private static readonly MissionId _win = new("WIN_1");

    private readonly MagicOnionTestClient _client = new();
    private readonly IProfileRepository _profiles = Substitute.For<IProfileRepository>();
    private readonly IMissionRepository _missions = Substitute.For<IMissionRepository>();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task GetMissionsAsync_AnswersWithEveryMissionMasterDataHoldsInItsOwnOrder()
    {
        var userId = Played();

        var missions = await CreateClient(userId).GetMissionsAsync();

        Assert.Equal(5, missions.Length);
        Assert.Equal(
            MasterData().MissionTable.All.Select(mission => mission.MissionId),
            missions.Select(item => item.Mission.MissionId));
    }

    [Fact]
    public async Task GetMissionsAsync_ReadsADayWithoutARowAsUntouched()
    {
        var userId = Played();

        var missions = await CreateClient(userId).GetMissionsAsync();

        Assert.All(missions, item => Assert.Equal(0, item.Progress));
        Assert.All(missions, item => Assert.False(item.Claimed));
    }

    [Fact]
    public async Task GetMissionsAsync_MergesTheRowsTodayAlreadyHasIntoTheirMissions()
    {
        var userId = Played(
            new MissionProgress(default, default, _play) { Progress = 2 },
            new MissionProgress(default, default, _win) { Progress = 1, Claimed = true });

        var missions = await CreateClient(userId).GetMissionsAsync();

        var play = Assert.Single(missions, item => item.Mission.MissionId == _play);
        Assert.Equal(2, play.Progress);
        Assert.False(play.Claimed);

        var win = Assert.Single(missions, item => item.Mission.MissionId == _win);
        Assert.Equal(1, win.Progress);
        Assert.True(win.Claimed);

        var untouched = Assert.Single(missions, item => item.Mission.MissionId == new MissionId("CLEAR_40"));
        Assert.Equal(0, untouched.Progress);
    }

    [Fact]
    public async Task ClaimAsync_PaysAMissionAgainstTheGoalAndRewardMasterDataGivesIt()
    {
        var userId = Played();
        var mission = MasterData().MissionTable.FindByMissionId(_win);
        _missions
            .ClaimAsync(new UserId(userId), Arg.Any<DateOnly>(), _win, Arg.Any<int>(), Arg.Any<Coins>())
            .Returns(MissionClaimOutcome.Claimed);

        Assert.Equal(MissionClaimResult.Claimed, await CreateClient(userId).ClaimAsync(_win));
        await _missions.Received(1).ClaimAsync(
            new UserId(userId), Arg.Any<DateOnly>(), _win, mission.Goal, mission.RewardCoins);
    }

    [Fact]
    public async Task ClaimAsync_OfAMissionThatIsStillShortOfItsGoal_ReportsNotCompleted()
    {
        var userId = Played();
        _missions
            .ClaimAsync(Arg.Any<UserId>(), Arg.Any<DateOnly>(), _win, Arg.Any<int>(), Arg.Any<Coins>())
            .Returns(MissionClaimOutcome.NotCompleted);

        Assert.Equal(MissionClaimResult.NotCompleted, await CreateClient(userId).ClaimAsync(_win));
    }

    [Fact]
    public async Task ClaimAsync_OfAMissionThatAlreadyPaidToday_ReportsAlreadyClaimed()
    {
        var userId = Played();
        _missions
            .ClaimAsync(Arg.Any<UserId>(), Arg.Any<DateOnly>(), _win, Arg.Any<int>(), Arg.Any<Coins>())
            .Returns(MissionClaimOutcome.AlreadyClaimed);

        Assert.Equal(MissionClaimResult.AlreadyClaimed, await CreateClient(userId).ClaimAsync(_win));
    }

    [Fact]
    public async Task ClaimAsync_OfAMissionMasterDataDoesNotHave_NeverReachesTheStore()
    {
        var userId = Played();

        Assert.Equal(
            MissionClaimResult.UnknownMission,
            await CreateClient(userId).ClaimAsync(new MissionId("RETIRED")));

        await _missions.DidNotReceiveWithAnyArgs().ClaimAsync(default, default, default, default, default);
    }

    [Fact]
    public async Task GetMissionsAsync_WithoutAToken_IsRejected()
        => await AssertRejectedAsync(async client => await client.GetMissionsAsync());

    [Fact]
    public async Task ClaimAsync_WithoutAToken_IsRejected()
        => await AssertRejectedAsync(async client => await client.ClaimAsync(_win));

    // The host loads the same master data the servers run on, so goals and rewards are the real ones.
    private MemoryDatabase MasterData() => factory.Services.GetRequiredService<MemoryDatabase>();

    // Stands up an account whose day already holds whatever rows the test hands over.
    private Guid Played(params MissionProgress[] rows)
    {
        var userId = new UserId(Guid.NewGuid());

        _missions.GetDailyAsync(userId, Arg.Any<DateOnly>()).Returns(rows);

        return userId.AsPrimitive();
    }

    private async Task AssertRejectedAsync(Func<IMissionService, Task> call)
    {
        var client = _client.Create<IMissionService>(factory);

        var exception = await Assert.ThrowsAsync<RpcException>(() => call(client));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    private IMissionService CreateClient(Guid userId) =>
        _client.Create<IMissionService>(
            factory.WithMissions(_profiles, _missions),
            factory.CreateToken(userId, "player@example.com"));
}
