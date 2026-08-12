using MagicOnion;
using SharpCampus.Shared.Dtos;

namespace SharpCampus.Shared.Services;

/// <summary>
/// Account-scoped calls. Every method requires a bearer token in the <c>authorization</c> request header.
/// </summary>
public interface IAccountService : IService<IAccountService>
{
    /// <summary>
    /// Gets the account the caller's bearer token belongs to.
    /// </summary>
    /// <returns>The identifier and email address of the authenticated account.</returns>
    UnaryResult<IdentityResponse> GetMyIdentityAsync();
}
