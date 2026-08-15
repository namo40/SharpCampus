using Microsoft.EntityFrameworkCore;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Profiles;

namespace SharpCampus.Server.Common.Settlement;

// Thin on purpose: what the game paid out is SettlementCalculator's answer, and all this adds is the
// one transaction that makes the coins, the ratings and the two history rows land together.
public sealed class MatchSettlementService(
    SharpCampusDbContext db,
    IProfileRepository profiles,
    MemoryDatabase masterData) : IMatchSettlementService
{
    public async Task<MatchSettlement> SettleAsync(MatchSettlementRequest request)
    {
        var first = request.Player1.UserId;
        var second = request.Player2.UserId;

        // A player can reach a match without ever touching a meta API, since the pairing worker seats
        // profileless accounts too. The settlement materializes both the way the meta services do.
        await profiles.CreateIfAbsentAsync(first, NicknameRules.CreateInitial(first));
        await profiles.CreateIfAbsentAsync(second, NicknameRules.CreateInitial(second));

        var accounts = await db.Profiles
            .AsNoTracking()
            .Where(p => p.UserId == first || p.UserId == second)
            .ToDictionaryAsync(p => p.UserId);

        var settlement = SettlementCalculator.Calculate(
            request,
            accounts[first],
            accounts[second],
            masterData.EconomyTable.All[0]);

        await using var transaction = await db.Database.BeginTransactionAsync();

        await AwardAsync(settlement.Player1);
        await AwardAsync(settlement.Player2);

        db.MatchRecords.AddRange(settlement.Player1.Record, settlement.Player2.Record);
        await db.SaveChangesAsync();

        await transaction.CommitAsync();

        return settlement;
    }

    private Task AwardAsync(SettledPlayer player) => db.Profiles
        .Where(p => p.UserId == player.UserId)
        .ExecuteUpdateAsync(setters => setters
            .SetProperty(p => p.Coins, player.Coins)
            .SetProperty(p => p.Rating, player.Rating)
            // A lambda so the database's own clock stamps the row, the same way a rename does.
            .SetProperty(p => p.UpdatedAt, _ => DateTimeOffset.UtcNow));
}
