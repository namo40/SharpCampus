using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Profiles;
using SharpCampus.Shared.Services;

namespace SharpCampus.Server.Common.Services;

[Authorize]
public sealed class ProfileService(IUserContext userContext, IProfileRepository profiles)
    : ServiceBase<IProfileService>, IProfileService
{
    public async UnaryResult<NicknameUpdateResult> UpdateNicknameAsync(string nickname)
    {
        // The client validates too, but only the server's verdict is binding.
        if (!NicknameRules.IsValid(nickname))
        {
            return NicknameUpdateResult.Invalid;
        }

        var userId = userContext.UserId;

        // A rename can be the account's first profile access, so materialize the profile the way the profile read does;
        // otherwise the UPDATE matches nothing and the rename silently evaporates.
        await profiles.CreateIfAbsentAsync(userId, NicknameRules.CreateInitial(userId));

        return await profiles.UpdateNicknameAsync(userId, nickname)
            ? NicknameUpdateResult.Updated
            : NicknameUpdateResult.Duplicate;
    }
}
