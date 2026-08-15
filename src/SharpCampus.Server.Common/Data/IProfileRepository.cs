using SharpCampus.Shared.Identity;

namespace SharpCampus.Server.Common.Data;

public interface IProfileRepository
{
    Task<Profile?> GetAsync(UserId userId);

    // One query for a whole page of a board; accounts without a profile row are simply absent.
    Task<IReadOnlyDictionary<UserId, string>> GetNicknamesAsync(IReadOnlyCollection<UserId> userIds);

    Task CreateIfAbsentAsync(UserId userId, string nickname);

    // Returns false when another account already holds the nickname.
    Task<bool> UpdateNicknameAsync(UserId userId, string nickname);
}
