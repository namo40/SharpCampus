using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Services;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public sealed class AccountServiceTests(SupabaseTestFactory factory) : IClassFixture<SupabaseTestFactory>, IDisposable
{
    private readonly List<GrpcChannel> _channels = [];

    public void Dispose()
    {
        foreach (var channel in _channels)
        {
            channel.Dispose();
        }
    }

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
    public async Task GetStatusAsync_StaysAnonymous()
    {
        var channel = GrpcChannel.ForAddress(
            factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() });
        _channels.Add(channel);

        var status = await MagicOnionClient.Create<IStatusService>(channel).GetStatusAsync();

        Assert.Equal("SharpCampus.ApiServer", status.ServerName);
    }

    private IAccountService CreateClient(string? token)
    {
        var channel = GrpcChannel.ForAddress(
            factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() });
        _channels.Add(channel);

        var client = MagicOnionClient.Create<IAccountService>(channel);

        return token is null
            ? client
            : client.WithHeaders(new Metadata { { "authorization", $"Bearer {token}" } });
    }
}
