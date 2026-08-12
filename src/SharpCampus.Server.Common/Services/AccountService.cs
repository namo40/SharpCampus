using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.Authorization;
using SharpCampus.Server.Common.Authentication;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Services;

namespace SharpCampus.Server.Common.Services;

[Authorize]
public sealed class AccountService(IUserContext userContext) : ServiceBase<IAccountService>, IAccountService
{
    public UnaryResult<IdentityResponse> GetMyIdentityAsync() =>
        UnaryResult.FromResult(new IdentityResponse(userContext.UserId, userContext.Email));
}
