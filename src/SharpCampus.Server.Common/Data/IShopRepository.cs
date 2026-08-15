using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

public interface IShopRepository
{
    // Free skins are not in here: only what an account paid for is a row.
    Task<IReadOnlyCollection<SkinId>> GetOwnedSkinsAsync(UserId userId);

    // The price is passed in because master data decides what a skin costs, not the row being written.
    Task<SkinPurchaseOutcome> PurchaseAsync(UserId userId, SkinId skinId, Coins price);

    Task EquipAsync(UserId userId, SkinId skinId);
}
