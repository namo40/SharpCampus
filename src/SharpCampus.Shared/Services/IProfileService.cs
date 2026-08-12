using MagicOnion;
using SharpCampus.Shared.Dtos;

namespace SharpCampus.Shared.Services;

/// <summary>
/// Changes to the caller's profile. Every method requires a bearer token in the <c>authorization</c> request header.
/// </summary>
public interface IProfileService : IService<IProfileService>
{
    /// <summary>
    /// Renames the caller's profile. Nicknames are unique across accounts, compared case-insensitively.
    /// </summary>
    /// <param name="nickname">Requested nickname, in the shape <see cref="Profiles.NicknameRules"/> accepts.</param>
    /// <returns>Whether the profile was renamed, and why not when it was not.</returns>
    UnaryResult<NicknameUpdateResult> UpdateNicknameAsync(string nickname);
}
