using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using ZLinq;

namespace SharpCampus.Server.Common.Services;

[Authorize]
public sealed class MatchHistoryService(
    IUserContext userContext,
    IProfileRepository profiles,
    IMatchHistoryRepository history)
    : ServiceBase<IMatchHistoryService>, IMatchHistoryService
{
    private const int RecentCount = 20;

    public async UnaryResult<MatchHistoryEntry[]> GetMyMatchesAsync()
    {
        // Nothing here writes, so an account that has never played is answered without a profile row
        // being created for it.
        var userId = userContext.UserId;
        var rows = await history.GetRecentAsync(userId, RecentCount);

        HashSet<UserId> opponents =
        [
            .. rows.AsValueEnumerable()
                .Where(row => row.OpponentId.HasValue)
                .Select(row => row.OpponentId!.Value),
        ];

        var nicknames = await profiles.GetNicknamesAsync(opponents);
        var entries = new MatchHistoryEntry[rows.Count];

        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = ToEntry(rows[i], nicknames);
        }

        return entries;
    }

    private static MatchHistoryEntry ToEntry(MatchHistoryRow row, IReadOnlyDictionary<UserId, string> nicknames) =>
        new(
            row.Match.CreatedAt,
            NicknameOf(row.OpponentId, nicknames),
            row.Match.Outcome,
            row.Match.EndReason,
            row.Match.RatingBefore,
            row.Match.RatingAfter,
            row.Match.CoinsAwarded);

    private static string NicknameOf(UserId? opponentId, IReadOnlyDictionary<UserId, string> nicknames)
    {
        // Settlement writes both rows of a game in one transaction, so a game missing its other side is a
        // database that was reset under it. The row is still the caller's own game and is answered unnamed
        // rather than withheld.
        if (opponentId is not { } id)
        {
            return string.Empty;
        }

        // Settling a match materializes a profile for both players, so an opponent without a row only
        // happens the same way.
        return nicknames.TryGetValue(id, out var nickname) ? nickname : NicknameRules.CreateInitial(id);
    }
}
