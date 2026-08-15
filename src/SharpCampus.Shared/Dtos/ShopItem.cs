using MessagePack;
using SharpCampus.Shared.MasterData;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// One catalog entry: the skin itself and where the caller stands with it.
/// </summary>
/// <param name="Skin">The master data record, sent whole because the client carries no master data of its own.</param>
/// <param name="Owned">Whether the caller may equip it. Every account owns the skins that cost nothing.</param>
/// <param name="Equipped">Whether this is the skin the caller's board is drawn with.</param>
[MessagePackObject]
public sealed record ShopItem(
    [property: Key(0)] Skin Skin,
    [property: Key(1)] bool Owned,
    [property: Key(2)] bool Equipped);
