using MessagePack;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// The persistent player profile behind an account.
/// </summary>
/// <param name="UserId">Identifier of the account the profile belongs to.</param>
/// <param name="Nickname">Name shown to opponents.</param>
/// <param name="Coins">Soft currency balance.</param>
/// <param name="Rating">Matchmaking skill score.</param>
/// <param name="EquippedSkinId">Skin the account's board is drawn with.</param>
[MessagePackObject]
public sealed record ProfileResponse(
    [property: Key(0)] UserId UserId,
    [property: Key(1)] string Nickname,
    [property: Key(2)] Coins Coins,
    [property: Key(3)] Rating Rating,
    [property: Key(4)] SkinId EquippedSkinId);
