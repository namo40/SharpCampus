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
public sealed class AccountService(IUserContext userContext, IProfileRepository profiles)
    : ServiceBase<IAccountService>, IAccountService
{
    public UnaryResult<IdentityResponse> GetMyIdentityAsync() =>
        UnaryResult.FromResult(new IdentityResponse(userContext.UserId, userContext.Email));

    public async UnaryResult<ProfileResponse> GetMyProfileAsync()
    {
        var userId = userContext.UserId;

        // Supabase owns the account, so this read is where the account first becomes a player.
        await profiles.CreateIfAbsentAsync(userId, NicknameRules.CreateInitial(userId));

        var profile = await profiles.GetAsync(userId)
                      ?? throw new InvalidOperationException($"Profile '{userId}' disappeared right after it was created.");

        return new ProfileResponse(userId, profile.Nickname, profile.Coins, profile.Rating);
    }
}
