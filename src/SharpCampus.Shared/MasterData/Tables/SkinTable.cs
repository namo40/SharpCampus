namespace SharpCampus.Shared.MasterData.Tables;

/// <summary>Hand-written half of the generated skin table: answers derived once from the whole catalog.</summary>
public sealed partial class SkinTable
{
    /// <summary>The skin every account owns and is created wearing: the first one that costs nothing.</summary>
    public Skin FreeSkin { get; private set; } = null!;

    // Resolved once when the table is built instead of rescanned by every fallback. A catalog without a
    // free skin leaves this null rather than throwing, so validation gets to report that data error itself.
    partial void OnAfterConstruct()
    {
        foreach (var skin in All)
        {
            if (skin.Price.AsPrimitive() == 0)
            {
                FreeSkin = skin;
                return;
            }
        }
    }
}
