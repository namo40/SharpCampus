using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// The shop as one answer: what the account has to spend and what there is to spend it on.
/// </summary>
/// <param name="Balance">Coin balance the prices are measured against.</param>
/// <param name="Items">Every skin master data holds, in master data order.</param>
[MessagePackObject]
public sealed record ShopResponse(
    [property: Key(0)] Coins Balance,
    [property: Key(1)] ShopItem[] Items);
