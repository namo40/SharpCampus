using Microsoft.EntityFrameworkCore;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

public sealed class MissionRepository(SharpCampusDbContext db) : IMissionRepository
{
    public async Task<IReadOnlyList<MissionProgress>> GetDailyAsync(UserId userId, DateOnly date) =>
        await db.MissionProgress
            .AsNoTracking()
            .Where(row => row.UserId == userId && row.MissionDate == date)
            .ToListAsync();

    public async Task RecordAsync(UserId userId, DateOnly date, IReadOnlyList<MissionDelta> deltas)
    {
        // Raw SQL because EF Core has no upsert primitive: ExecuteUpdate cannot insert the row it misses,
        // and insert-or-increment has to be one atomic statement. The value objects are unwrapped here
        // because raw SQL goes around the converters that would otherwise do it.
        foreach (var delta in deltas)
        {
            await db.Database.ExecuteSqlAsync(
                $"""
                 INSERT INTO mission_progress (user_id, mission_date, mission_id, progress)
                 VALUES ({userId.AsPrimitive()}, {date}, {delta.MissionId.AsPrimitive()}, {delta.Amount})
                 ON CONFLICT (user_id, mission_date, mission_id)
                 DO UPDATE SET progress = mission_progress.progress + EXCLUDED.progress, updated_at = now()
                 """);
        }
    }

    public async Task<MissionClaimOutcome> ClaimAsync(
        UserId userId,
        DateOnly date,
        MissionId missionId,
        int goal,
        Coins reward)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        // The unclaimed condition is the whole guard against paying twice: two claims at once both run
        // this update, and only the one that flips the flag has a reward to credit.
        var claimed = await db.MissionProgress
            .Where(row => row.UserId == userId
                          && row.MissionDate == date
                          && row.MissionId == missionId
                          && !row.Claimed
                          && row.Progress >= goal)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Claimed, true)
                // A lambda so the database's own clock stamps the row, the same way a rename does.
                .SetProperty(row => row.UpdatedAt, _ => DateTimeOffset.UtcNow));

        if (claimed == 0)
        {
            // Nothing was written, so there is nothing to commit and the row only has to say why.
            var row = await db.MissionProgress
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.UserId == userId && r.MissionDate == date && r.MissionId == missionId);

            return row is { Claimed: true } ? MissionClaimOutcome.AlreadyClaimed : MissionClaimOutcome.NotCompleted;
        }

        await db.Profiles
            .Where(profile => profile.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(profile => profile.Coins, profile => profile.Coins + reward)
                .SetProperty(profile => profile.UpdatedAt, _ => DateTimeOffset.UtcNow));

        await transaction.CommitAsync();
        return MissionClaimOutcome.Claimed;
    }
}
