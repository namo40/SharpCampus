using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Services;

[Authorize]
public sealed class ShopService(
    IUserContext userContext,
    IProfileRepository profiles,
    IShopRepository shop,
    MemoryDatabase masterData)
    : ServiceBase<IShopService>, IShopService
{
    public async UnaryResult<ShopResponse> GetShopAsync()
    {
        var userId = await MaterializeProfileAsync();

        var profile = await profiles.GetAsync(userId)
                      ?? throw new InvalidOperationException($"Profile '{userId}' disappeared right after it was created.");

        var owned = await shop.GetOwnedSkinsAsync(userId);
        var catalog = masterData.SkinTable.All;
        var items = new ShopItem[catalog.Count];

        for (var i = 0; i < items.Length; i++)
        {
            var skin = catalog[i];
            items[i] = new ShopItem(skin, IsOwned(skin, owned), skin.SkinId == profile.EquippedSkinId);
        }

        return new ShopResponse(profile.Coins, items);
    }

    public async UnaryResult<SkinPurchaseResult> PurchaseAsync(SkinId skinId)
    {
        var userId = await MaterializeProfileAsync();

        if (!masterData.SkinTable.TryFindBySkinId(skinId, out var skin))
        {
            return SkinPurchaseResult.UnknownSkin;
        }

        // Nothing to sell: a skin that costs nothing comes with the account.
        if (skin.Price.AsPrimitive() == 0)
        {
            return SkinPurchaseResult.AlreadyOwned;
        }

        return await shop.PurchaseAsync(userId, skinId, skin.Price) switch
        {
            SkinPurchaseOutcome.Purchased => SkinPurchaseResult.Purchased,
            SkinPurchaseOutcome.InsufficientCoins => SkinPurchaseResult.InsufficientCoins,
            _ => SkinPurchaseResult.AlreadyOwned,
        };
    }

    public async UnaryResult<SkinEquipResult> EquipAsync(SkinId skinId)
    {
        var userId = await MaterializeProfileAsync();

        if (!masterData.SkinTable.TryFindBySkinId(skinId, out var skin))
        {
            return SkinEquipResult.UnknownSkin;
        }

        // A free skin is owned without a row, so only a paid one is worth a lookup. Nothing ever takes a
        // skin away either, so an answer that is a moment old is still true when the update lands.
        if (skin.Price.AsPrimitive() > 0 && !(await shop.GetOwnedSkinsAsync(userId)).Contains(skinId))
        {
            return SkinEquipResult.NotOwned;
        }

        await shop.EquipAsync(userId, skinId);
        return SkinEquipResult.Equipped;
    }

    private static bool IsOwned(Skin skin, IReadOnlyCollection<SkinId> owned) =>
        skin.Price.AsPrimitive() == 0 || owned.Contains(skin.SkinId);

    // Any of these calls can be the account's first, so the profile is materialized the way a profile
    // read does it; otherwise the balance and the equipped skin would have nowhere to live.
    private async Task<UserId> MaterializeProfileAsync()
    {
        var userId = userContext.UserId;
        await profiles.CreateIfAbsentAsync(userId, NicknameRules.CreateInitial(userId));

        return userId;
    }
}
