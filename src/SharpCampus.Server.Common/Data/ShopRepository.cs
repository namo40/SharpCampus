using Microsoft.EntityFrameworkCore;
using Npgsql;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

public sealed class ShopRepository(SharpCampusDbContext db) : IShopRepository
{
    public async Task<IReadOnlyCollection<SkinId>> GetOwnedSkinsAsync(UserId userId) =>
        await db.OwnedSkins
            .AsNoTracking()
            .Where(skin => skin.UserId == userId)
            .Select(skin => skin.SkinId)
            .ToListAsync();

    public async Task<SkinPurchaseOutcome> PurchaseAsync(UserId userId, SkinId skinId, Coins price)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var entry = db.OwnedSkins.Add(new OwnedSkin(userId, skinId));

        // Nothing is read before either write: the primary key settles who bought the skin first and the
        // conditional update settles who could afford it, the same way the nickname index settles a rename.
        // A rejected insert leaves the transaction unusable, so the only thing left is to let it roll back.
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            entry.State = EntityState.Detached;
            return SkinPurchaseOutcome.AlreadyOwned;
        }

        var debited = await db.Profiles
            .Where(p => p.UserId == userId && p.Coins >= price)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Coins, p => p.Coins - price)
                // A lambda so the database's own clock stamps the row, the same way a rename does.
                .SetProperty(p => p.UpdatedAt, _ => DateTimeOffset.UtcNow));

        if (debited == 0)
        {
            return SkinPurchaseOutcome.InsufficientCoins;
        }

        await transaction.CommitAsync();
        return SkinPurchaseOutcome.Purchased;
    }

    public Task EquipAsync(UserId userId, SkinId skinId) => db.Profiles
        .Where(profile => profile.UserId == userId)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(profile => profile.EquippedSkinId, skinId)
            // A lambda so the database's own clock stamps the row, the same way a rename does.
            .SetProperty(profile => profile.UpdatedAt, _ => DateTimeOffset.UtcNow));

    // SaveChanges wraps the violation the way a duplicate profile insert surfaces it.
    private static bool IsUniqueViolation(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.SqlState == PostgresErrorCodes.UniqueViolation;
}
