using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ApiServer.Tests;

public sealed class ShopServiceTests(SupabaseTestFactory factory) : IClassFixture<SupabaseTestFactory>, IDisposable
{
    private static readonly SkinId _free = new("CLASSIC");
    private static readonly SkinId _paid = new("MONO");

    private readonly MagicOnionTestClient _client = new();
    private readonly IProfileRepository _profiles = Substitute.For<IProfileRepository>();
    private readonly IShopRepository _shop = Substitute.For<IShopRepository>();

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task GetShopAsync_ReturnsTheWholeCatalogAgainstTheAccountsBalance()
    {
        var userId = Owns(new Coins(250));

        var shop = await CreateClient(userId).GetShopAsync();

        Assert.Equal(new Coins(250), shop.Balance);
        Assert.Equal(6, shop.Items.Length);
        Assert.Equal(
            MasterData().SkinTable.All.Select(skin => skin.SkinId),
            shop.Items.Select(item => item.Skin.SkinId));
    }

    [Fact]
    public async Task GetShopAsync_ShowsTheFreeSkinAsOwnedAndWornWithoutAnythingBeingBought()
    {
        var userId = Owns(new Coins(0));

        var shop = await CreateClient(userId).GetShopAsync();

        var free = Assert.Single(shop.Items, item => item.Skin.SkinId == _free);
        Assert.True(free.Owned);
        Assert.True(free.Equipped);

        var paid = Assert.Single(shop.Items, item => item.Skin.SkinId == _paid);
        Assert.False(paid.Owned);
        Assert.False(paid.Equipped);
    }

    [Fact]
    public async Task GetShopAsync_FlagsAPaidSkinTheAccountBought()
    {
        var userId = Owns(new Coins(0), _paid);

        var shop = await CreateClient(userId).GetShopAsync();

        var paid = Assert.Single(shop.Items, item => item.Skin.SkinId == _paid);
        Assert.True(paid.Owned);
        Assert.False(paid.Equipped);
    }

    [Fact]
    public async Task PurchaseAsync_ChargesThePriceMasterDataGivesTheSkin()
    {
        var userId = Owns(new Coins(1000));
        _shop.PurchaseAsync(new UserId(userId), _paid, Arg.Any<Coins>()).Returns(SkinPurchaseOutcome.Purchased);

        var result = await CreateClient(userId).PurchaseAsync(_paid);

        Assert.Equal(SkinPurchaseResult.Purchased, result);
        await _shop.Received(1).PurchaseAsync(
            new UserId(userId), _paid, MasterData().SkinTable.FindBySkinId(_paid).Price);
    }

    [Fact]
    public async Task PurchaseAsync_OfASkinTheAccountAlreadyHasARowFor_ReportsAlreadyOwned()
    {
        var userId = Owns(new Coins(1000));
        _shop.PurchaseAsync(Arg.Any<UserId>(), Arg.Any<SkinId>(), Arg.Any<Coins>())
            .Returns(SkinPurchaseOutcome.AlreadyOwned);

        Assert.Equal(SkinPurchaseResult.AlreadyOwned, await CreateClient(userId).PurchaseAsync(_paid));
    }

    [Fact]
    public async Task PurchaseAsync_OfAFreeSkin_IsAnsweredWithoutTouchingTheStore()
    {
        var userId = Owns(new Coins(0));

        Assert.Equal(SkinPurchaseResult.AlreadyOwned, await CreateClient(userId).PurchaseAsync(_free));
        await _shop.DidNotReceiveWithAnyArgs().PurchaseAsync(default, default, default);
    }

    [Fact]
    public async Task PurchaseAsync_WithoutTheCoinsToCoverIt_ReportsInsufficientCoins()
    {
        var userId = Owns(new Coins(0));
        _shop.PurchaseAsync(Arg.Any<UserId>(), Arg.Any<SkinId>(), Arg.Any<Coins>())
            .Returns(SkinPurchaseOutcome.InsufficientCoins);

        Assert.Equal(SkinPurchaseResult.InsufficientCoins, await CreateClient(userId).PurchaseAsync(_paid));
    }

    [Fact]
    public async Task PurchaseAsync_OfASkinMasterDataDoesNotHave_NeverReachesTheStore()
    {
        var userId = Owns(new Coins(1000));

        Assert.Equal(SkinPurchaseResult.UnknownSkin, await CreateClient(userId).PurchaseAsync(new SkinId("RETIRED")));
        await _shop.DidNotReceiveWithAnyArgs().PurchaseAsync(default, default, default);
    }

    [Fact]
    public async Task EquipAsync_PutsOnASkinTheAccountBought()
    {
        var userId = Owns(new Coins(0), _paid);

        Assert.Equal(SkinEquipResult.Equipped, await CreateClient(userId).EquipAsync(_paid));
        await _shop.Received(1).EquipAsync(new UserId(userId), _paid);
    }

    [Fact]
    public async Task EquipAsync_OfAFreeSkin_NeedsNoOwnershipRow()
    {
        var userId = Owns(new Coins(0));

        Assert.Equal(SkinEquipResult.Equipped, await CreateClient(userId).EquipAsync(_free));
        await _shop.Received(1).EquipAsync(new UserId(userId), _free);
    }

    [Fact]
    public async Task EquipAsync_OfAPaidSkinTheAccountNeverBought_ReportsNotOwned()
    {
        var userId = Owns(new Coins(0));

        Assert.Equal(SkinEquipResult.NotOwned, await CreateClient(userId).EquipAsync(_paid));
        await _shop.DidNotReceiveWithAnyArgs().EquipAsync(default, default);
    }

    [Fact]
    public async Task EquipAsync_OfASkinMasterDataDoesNotHave_ReportsUnknownSkin()
    {
        var userId = Owns(new Coins(0), _paid);

        Assert.Equal(SkinEquipResult.UnknownSkin, await CreateClient(userId).EquipAsync(new SkinId("RETIRED")));
        await _shop.DidNotReceiveWithAnyArgs().EquipAsync(default, default);
    }

    [Fact]
    public async Task GetShopAsync_WithoutAToken_IsRejected()
        => await AssertRejectedAsync(async client => await client.GetShopAsync());

    [Fact]
    public async Task PurchaseAsync_WithoutAToken_IsRejected()
        => await AssertRejectedAsync(async client => await client.PurchaseAsync(_paid));

    [Fact]
    public async Task EquipAsync_WithoutAToken_IsRejected()
        => await AssertRejectedAsync(async client => await client.EquipAsync(_paid));

    // The host loads the same master data the servers run on, so prices and keys are the real ones.
    private MemoryDatabase MasterData() => factory.Services.GetRequiredService<MemoryDatabase>();

    // Stands up an account wearing the free skin, with a balance and whatever it has already bought.
    private Guid Owns(Coins balance, params SkinId[] skins)
    {
        var userId = new UserId(Guid.NewGuid());

        _profiles.GetAsync(userId).Returns(new Profile(userId, NicknameRules.CreateInitial(userId))
        {
            Coins = balance,
            EquippedSkinId = _free,
        });

        _shop.GetOwnedSkinsAsync(userId).Returns(skins);

        return userId.AsPrimitive();
    }

    private async Task AssertRejectedAsync(Func<IShopService, Task> call)
    {
        var client = _client.Create<IShopService>(factory);

        var exception = await Assert.ThrowsAsync<RpcException>(() => call(client));

        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    private IShopService CreateClient(Guid userId) =>
        _client.Create<IShopService>(
            factory.WithShop(_profiles, _shop),
            factory.CreateToken(userId, "player@example.com"));
}
