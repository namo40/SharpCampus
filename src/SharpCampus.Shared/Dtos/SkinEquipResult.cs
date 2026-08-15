namespace SharpCampus.Shared.Dtos;

/// <summary>
/// Outcome of equipping a skin.
/// </summary>
public enum SkinEquipResult
{
    /// <summary>The profile now carries the requested skin.</summary>
    Equipped,

    /// <summary>The skin has to be bought before it can be equipped.</summary>
    NotOwned,

    /// <summary>Master data holds no skin under that identifier.</summary>
    UnknownSkin,
}
