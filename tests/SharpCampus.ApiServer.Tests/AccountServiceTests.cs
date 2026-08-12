using Grpc.Core;
using NSubstitute;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public sealed class AccountServiceTests(SupabaseTestFactory factory) : IClassFixture<SupabaseTestFactory>, IDisposable
{
    private readonly MagicOnionTestClient _client = new();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task GetMyIdentityAsync_ReturnsTheClaimsOfTheBearerToken()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(factory.CreateToken(userId, "player@example.com"));

        var identity = await client.GetMyIdentityAsync();

        Assert.Equal(new UserId(userId), identity.UserId);
        Assert.Equal("player@example.com", identity.Email);
    }

    [Fact]
    public async Task GetMyIdentityAsync_WithoutAToken_IsRejected()
    {
        var client = CreateClient(token: null);

        var exception = await Assert.ThrowsAsync<RpcException>(async () => await client.GetMyIdentityAsync());

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task GetMyIdentityAsync_WithATokenSignedByAnotherKey_IsRejected()
    {
        var client = CreateClient(factory.CreateForeignlySignedToken(Guid.NewGuid(), "player@example.com"));

        var exception = await Assert.ThrowsAsync<RpcException>(async () => await client.GetMyIdentityAsync());

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task GetMyIdentityAsync_WithAnExpiredToken_IsRejected()
    {
        var client = CreateClient(factory.CreateExpiredToken(Guid.NewGuid(), "player@example.com"));

        var exception = await Assert.ThrowsAsync<RpcException>(async () => await client.GetMyIdentityAsync());

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task GetMyProfileAsync_CreatesTheProfileForTheAccountInTheToken()
    {
        var userId = Guid.NewGuid();
        var profiles = Substitute.For<IProfileRepository>();
        profiles.GetAsync(new UserId(userId)).Returns(Stored(userId, "player_0123abcd"));

        await CreateClient(profiles, userId).GetMyProfileAsync();

        await profiles.Received(1).CreateIfAbsentAsync(
            new UserId(userId),
            Arg.Is<string>(nickname => nickname.StartsWith(NicknameRules.InitialPrefix) && NicknameRules.IsValid(nickname)));
    }

    [Fact]
    public async Task GetMyProfileAsync_ReturnsTheStoredProfile()
    {
        var userId = Guid.NewGuid();
        var profiles = Substitute.For<IProfileRepository>();
        profiles.GetAsync(new UserId(userId)).Returns(Stored(userId, "boardsweeper"));

        var profile = await CreateClient(profiles, userId).GetMyProfileAsync();

        Assert.Equal(new UserId(userId), profile.UserId);
        Assert.Equal("boardsweeper", profile.Nickname);
        Assert.Equal(new Coins(120), profile.Coins);
        Assert.Equal(new Rating(1180), profile.Rating);
    }

    [Fact]
    public async Task GetMyProfileAsync_WithoutAToken_IsRejected()
    {
        var client = CreateClient(token: null);

        var exception = await Assert.ThrowsAsync<RpcException>(async () => await client.GetMyProfileAsync());

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task GetStatusAsync_StaysAnonymous()
    {
        var status = await _client.Create<IStatusService>(factory).GetStatusAsync();

        Assert.Equal("SharpCampus.ApiServer", status.ServerName);
    }

    private static Profile Stored(Guid userId, string nickname) =>
        new(new UserId(userId), nickname) { Coins = new Coins(120), Rating = new Rating(1180) };

    private IAccountService CreateClient(string? token) =>
        _client.Create<IAccountService>(factory, token);

    private IAccountService CreateClient(IProfileRepository profiles, Guid userId) =>
        _client.Create<IAccountService>(
            factory.WithProfiles(profiles),
            factory.CreateToken(userId, "player@example.com"));
}
