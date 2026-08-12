using SharpCampus.Shared.Identity;

namespace SharpCampus.Server.Common.Data;

public interface IProfileRepository
{
    Task<Profile?> GetAsync(UserId userId);

    Task CreateIfAbsentAsync(UserId userId, string nickname);

    // Returns false when another account already holds the nickname.
    Task<bool> UpdateNicknameAsync(UserId userId, string nickname);
}
