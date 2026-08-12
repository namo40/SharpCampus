using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using SharpCampus.Shared.Identity;

namespace SharpCampus.Server.Common.Authentication;

public sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    public UserId UserId => UserId.Parse(RequiredClaim(JwtRegisteredClaimNames.Sub));

    public string Email => RequiredClaim(JwtRegisteredClaimNames.Email);

    // Only reachable from an authorized call, so a missing claim means the token shape changed under us.
    private string RequiredClaim(string claimType) =>
        httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value
        ?? throw new InvalidOperationException($"The authenticated caller has no '{claimType}' claim.");
}
