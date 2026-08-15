using Microsoft.EntityFrameworkCore;
using Npgsql;
using SharpCampus.Shared.Identity;

namespace SharpCampus.Server.Common.Data;

public sealed class ProfileRepository(SharpCampusDbContext db) : IProfileRepository
{
    public Task<Profile?> GetAsync(UserId userId) =>
        db.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId);

    public async Task<IReadOnlyDictionary<UserId, string>> GetNicknamesAsync(IReadOnlyCollection<UserId> userIds) =>
        await db.Profiles
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.Nickname })
            .ToDictionaryAsync(p => p.UserId, p => p.Nickname);

    public async Task CreateIfAbsentAsync(UserId userId, string nickname)
    {
        if (await db.Profiles.AnyAsync(p => p.UserId == userId))
        {
            return;
        }

        var entry = db.Profiles.Add(new Profile(userId, nickname));

        // Two calls for the same account can pass the check together, so the primary key settles it:
        // the loser only learns that the profile it wanted is already there.
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (IsUniqueViolation(e))
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task<bool> UpdateNicknameAsync(UserId userId, string nickname)
    {
        // Two players can claim the same nickname at the same moment, so the unique index decides the winner
        // and the loser is recognised by its violation. A SELECT beforehand would only narrow the race.
        try
        {
            await db.Profiles
                .Where(p => p.UserId == userId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.Nickname, nickname)
                    // Written as a lambda so EF translates it to the database's now() instead of sending this
                    // server's clock, which keeps the timestamps of a scaled-out deployment comparable.
                    .SetProperty(p => p.UpdatedAt, _ => DateTimeOffset.UtcNow));

            return true;
        }
        catch (Exception e) when (IsUniqueViolation(e))
        {
            return false;
        }
    }

    // SaveChanges wraps the failure in DbUpdateException, while ExecuteUpdate lets Npgsql's own exception through.
    private static bool IsUniqueViolation(Exception exception) =>
        (exception as PostgresException ?? exception.InnerException as PostgresException)?.SqlState
        == PostgresErrorCodes.UniqueViolation;
}
