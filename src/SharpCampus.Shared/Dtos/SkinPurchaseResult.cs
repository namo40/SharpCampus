namespace SharpCampus.Shared.Dtos;

/// <summary>
/// Outcome of a skin purchase.
/// </summary>
public enum SkinPurchaseResult
{
    /// <summary>The skin is now owned and its price has been debited.</summary>
    Purchased,

    /// <summary>The account already owns the skin, so nothing was charged.</summary>
    AlreadyOwned,

    /// <summary>The balance does not cover the price, so nothing was charged.</summary>
    InsufficientCoins,

    /// <summary>Master data holds no skin under that identifier.</summary>
    UnknownSkin,
}
