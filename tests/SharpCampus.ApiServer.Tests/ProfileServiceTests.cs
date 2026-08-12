using Grpc.Core;
using NSubstitute;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public sealed class ProfileServiceTests(SupabaseTestFactory factory) : IClassFixture<SupabaseTestFactory>, IDisposable
{
    private readonly MagicOnionTestClient _client = new();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task UpdateNicknameAsync_StoresTheNicknameForTheAccountInTheToken()
    {
        var userId = Guid.NewGuid();
        var profiles = Substitute.For<IProfileRepository>();
        profiles.UpdateNicknameAsync(new UserId(userId), "boardsweeper").Returns(true);

        var result = await CreateClient(profiles, userId).UpdateNicknameAsync("boardsweeper");

        Assert.Equal(NicknameUpdateResult.Updated, result);
        await profiles.Received(1).CreateIfAbsentAsync(
            new UserId(userId), Arg.Is<string>(static nickname => NicknameRules.IsValid(nickname)));
    }

    [Fact]
    public async Task UpdateNicknameAsync_WhenTheUniqueIndexRejectsTheName_ReportsDuplicate()
    {
        var profiles = Substitute.For<IProfileRepository>();
        profiles.UpdateNicknameAsync(Arg.Any<UserId>(), Arg.Any<string>()).Returns(false);

        var result = await CreateClient(profiles, Guid.NewGuid()).UpdateNicknameAsync("boardsweeper");

        Assert.Equal(NicknameUpdateResult.Duplicate, result);
    }

    [Fact]
    public async Task UpdateNicknameAsync_WithAMalformedNickname_NeverReachesTheStore()
    {
        var profiles = Substitute.For<IProfileRepository>();

        var result = await CreateClient(profiles, Guid.NewGuid()).UpdateNicknameAsync("a");

        Assert.Equal(NicknameUpdateResult.Invalid, result);
        await profiles.DidNotReceiveWithAnyArgs().CreateIfAbsentAsync(default, string.Empty);
        await profiles.DidNotReceiveWithAnyArgs().UpdateNicknameAsync(default, string.Empty);
    }

    [Fact]
    public async Task UpdateNicknameAsync_WithoutAToken_IsRejected()
    {
        var client = _client.Create<IProfileService>(factory);

        var exception = await Assert.ThrowsAsync<RpcException>(async () => await client.UpdateNicknameAsync("boardsweeper"));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    private IProfileService CreateClient(IProfileRepository profiles, Guid userId) =>
        _client.Create<IProfileService>(
            factory.WithProfiles(profiles),
            factory.CreateToken(userId, "player@example.com"));
}
