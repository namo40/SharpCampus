using SharpCampus.Shared.Identity;

namespace SharpCampus.Server.Common.Authentication;

public interface IUserContext
{
    UserId UserId { get; }

    string Email { get; }
}
