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
public sealed class MissionService(
    IUserContext userContext,
    IProfileRepository profiles,
    IMissionRepository missions,
    MemoryDatabase masterData,
    TimeProvider time)
    : ServiceBase<IMissionService>, IMissionService
{
    public async UnaryResult<MissionItem[]> GetMissionsAsync()
    {
        var userId = await MaterializeProfileAsync();

        var progress = await missions.GetDailyAsync(userId, Today());
        var catalog = masterData.MissionTable.All;
        var items = new MissionItem[catalog.Count];

        for (var i = 0; i < items.Length; i++)
        {
            var mission = catalog[i];

            // A mission nothing has been done towards has no row of its own, which reads as a fresh zero.
            var row = progress.FirstOrDefault(r => r.MissionId == mission.MissionId);
            items[i] = new MissionItem(mission, row?.Progress ?? 0, row?.Claimed ?? false);
        }

        return items;
    }

    public async UnaryResult<MissionClaimResult> ClaimAsync(MissionId missionId)
    {
        var userId = await MaterializeProfileAsync();

        if (!masterData.MissionTable.TryFindByMissionId(missionId, out var mission))
        {
            return MissionClaimResult.UnknownMission;
        }

        return await missions.ClaimAsync(userId, Today(), missionId, mission.Goal, mission.RewardCoins) switch
        {
            MissionClaimOutcome.Claimed => MissionClaimResult.Claimed,
            MissionClaimOutcome.AlreadyClaimed => MissionClaimResult.AlreadyClaimed,
            _ => MissionClaimResult.NotCompleted,
        };
    }

    // The UTC date is the day everywhere: the room server stamps progress with it and this reads it back,
    // so neither side has to know where the caller is.
    private DateOnly Today() => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);

    // Any of these calls can be the account's first, so the profile is materialized the way a profile
    // read does it; otherwise a claimed reward would have no balance to land in.
    private async Task<UserId> MaterializeProfileAsync()
    {
        var userId = userContext.UserId;
        await profiles.CreateIfAbsentAsync(userId, NicknameRules.CreateInitial(userId));

        return userId;
    }
}
