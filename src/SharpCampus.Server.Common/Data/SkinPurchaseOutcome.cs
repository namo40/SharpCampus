namespace SharpCampus.Server.Common.Data;

// What the purchase transaction settled on. Server side only: the service maps it onto the contract's result.
public enum SkinPurchaseOutcome
{
    Purchased,
    AlreadyOwned,
    InsufficientCoins,
}
