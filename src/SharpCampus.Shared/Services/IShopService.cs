using MagicOnion;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Services;

/// <summary>
/// The skin shop, where coins are spent. Every method requires a bearer token in the <c>authorization</c> request header.
/// </summary>
public interface IShopService : IService<IShopService>
{
    /// <summary>
    /// Gets the whole catalog and the caller's balance in one read, so a price and the coins it is measured
    /// against always come from the same moment.
    /// </summary>
    /// <returns>The coin balance and every skin, each flagged as owned and equipped.</returns>
    UnaryResult<ShopResponse> GetShopAsync();

    /// <summary>
    /// Buys a skin for the caller and debits the price master data gives it.
    /// </summary>
    /// <param name="skinId">Skin to buy.</param>
    /// <returns>Whether the skin was bought, and why not when it was not.</returns>
    UnaryResult<SkinPurchaseResult> PurchaseAsync(SkinId skinId);

    /// <summary>
    /// Equips a skin the caller owns. Both screens of the caller's next match draw their board with it.
    /// </summary>
    /// <param name="skinId">Skin to equip.</param>
    /// <returns>Whether the skin was equipped, and why not when it was not.</returns>
    UnaryResult<SkinEquipResult> EquipAsync(SkinId skinId);
}
