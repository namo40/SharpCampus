using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Data;

// Only the columns the server owns are constructor parameters; the rest are filled by the database on insert.
public sealed record Profile(UserId UserId, string Nickname)
{
    public Coins Coins { get; init; }

    public Rating Rating { get; init; }

    public SkinId EquippedSkinId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
