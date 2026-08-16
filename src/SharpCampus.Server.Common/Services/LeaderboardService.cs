using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Server.Common.Data;
using SharpCampus.Server.Common.Leaderboards;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;
using ZLinq;

namespace SharpCampus.Server.Common.Services;

[Authorize]
public sealed class LeaderboardService(
    IUserContext userContext,
    IProfileRepository profiles,
    ILeaderboardStore leaderboard,
    ILeaderboardCache cache,
    TimeProvider time)
    : ServiceBase<ILeaderboardService>, ILeaderboardService
{
    private const int TopCount = 100;

    public async UnaryResult<LeaderboardView> GetLeaderboardAsync(LeaderboardKind kind)
    {
        // Nothing here writes, so an account that has never played is answered without a profile row
        // being created for it.
        var userId = userContext.UserId;
        var board = BoardOf(kind);

        // The caller's own place is personal to the call and one cheap Redis read, so it is never served
        // from the cached page beside it.
        var me = await leaderboard.FindAsync(board, userId);

        if (await cache.GetTopAsync(board) is { } cached)
        {
            // The page already carries its names, leaving only the one beside the caller's own place.
            return new LeaderboardView(
                cached,
                me is null ? null : ToEntry(me, await profiles.GetNicknamesAsync([me.UserId])));
        }

        var top = await leaderboard.TopAsync(board, TopCount);

        HashSet<UserId> ids = [.. top.AsValueEnumerable().Select(row => row.UserId)];
        if (me is not null)
        {
            ids.Add(me.UserId);
        }

        var nicknames = await profiles.GetNicknamesAsync(ids);
        var entries = new LeaderboardEntry[top.Length];

        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = ToEntry(top[i], nicknames);
        }

        await cache.SetTopAsync(board, entries);

        return new LeaderboardView(entries, me is null ? null : ToEntry(me, nicknames));
    }

    // Redis counts places from zero and players count them from one.
    private static LeaderboardEntry ToEntry(LeaderboardRow row, IReadOnlyDictionary<UserId, string> nicknames) => new(
        (int)row.Rank + 1,
        // Settling a match materializes a profile for both players, so a board member without a row only
        // happens when Redis outlives a database that was reset under it.
        nicknames.TryGetValue(row.UserId, out var nickname) ? nickname : NicknameRules.CreateInitial(row.UserId),
        row.Score);

    private string BoardOf(LeaderboardKind kind) => kind switch
    {
        // The day is the UTC date the room server stamped the win with, so neither side has to know
        // where the caller is.
        LeaderboardKind.DailyWins => LeaderboardKeys.Daily(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime)),
        _ => LeaderboardKeys.Rating,
    };
}
