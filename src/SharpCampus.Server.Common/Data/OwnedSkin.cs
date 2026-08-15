using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

// One row per bought skin. Free skins never get one: every account owns them already.
public sealed record OwnedSkin(UserId UserId, SkinId SkinId)
{
    public DateTimeOffset AcquiredAt { get; init; }
}
