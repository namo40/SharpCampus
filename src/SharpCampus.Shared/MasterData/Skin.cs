using MasterMemory;
using MessagePack;
using SharpCampus.Shared.Values;

namespace SharpCampus.Shared.MasterData;

/// <summary>
/// A cosmetic block theme: the glyph a cell is drawn with and the color of every piece.
/// </summary>
[MemoryTable("skins")]
[MessagePackObject(true)]
public sealed record Skin : IValidatable<Skin>
{
    // One board cell is drawn as two console columns, so every glyph has to be exactly that wide
    // or the two boards stop lining up.
    /// <summary>Console columns one board cell occupies.</summary>
    public const int GlyphWidth = 2;

    /// <summary>Primary key: the identifier an account owns and equips.</summary>
    [PrimaryKey] public SkinId SkinId { get; init; }

    /// <summary>Localization key of the display name.</summary>
    public string NameKey { get; init; } = "";

    /// <summary>Price in coins; zero for the skin every account starts with.</summary>
    public Coins Price { get; init; }

    /// <summary>Characters one filled cell is drawn with, exactly <see cref="GlyphWidth"/> wide.</summary>
    public string BlockGlyph { get; init; } = "";

    /// <summary>Color of the I piece.</summary>
    public string ColorI { get; init; } = "";

    /// <summary>Color of the O piece.</summary>
    public string ColorO { get; init; } = "";

    /// <summary>Color of the T piece.</summary>
    public string ColorT { get; init; } = "";

    /// <summary>Color of the S piece.</summary>
    public string ColorS { get; init; } = "";

    /// <summary>Color of the Z piece.</summary>
    public string ColorZ { get; init; } = "";

    /// <summary>Color of the J piece.</summary>
    public string ColorJ { get; init; } = "";

    /// <summary>Color of the L piece.</summary>
    public string ColorL { get; init; } = "";

    /// <summary>Color of a received garbage cell.</summary>
    public string ColorGarbage { get; init; } = "";

    // A method rather than a property: MessagePackObject(true) would treat a property as one more column.
    /// <summary>Returns every color of this skin, piece colors first and garbage last.</summary>
    /// <returns>The color names in board rendering order.</returns>
    public string[] PaletteColors() => [ColorI, ColorO, ColorT, ColorS, ColorZ, ColorJ, ColorL, ColorGarbage];

    void IValidatable<Skin>.Validate(IValidator<Skin> validator)
    {
        validator.Validate(x => x.NameKey.Length > 0);
        validator.Validate(x => x.Price.AsPrimitive() >= 0);
        validator.Validate(x => x.BlockGlyph.Length == GlyphWidth);
        validator.Validate(
            x => Array.TrueForAll(x.PaletteColors(), color => color.Length > 0),
            "every palette color must be set");

        // The renderer turns these into terminal colors, so a typo has to be caught here rather than
        // surface as an unpainted board halfway through a match.
        validator.Validate(
            x => Array.TrueForAll(x.PaletteColors(), color => Enum.TryParse<ConsoleColor>(color, out _)),
            "every palette color must name a ConsoleColor");

        if (!validator.CallOnce())
        {
            return;
        }

        // The profile schema default and the fallback seat both assume a free skin exists, so a catalog
        // without one is a data error to catch here, not a crash to have at pairing time.
        var hasFreeSkin = false;
        foreach (var skin in validator.GetTableSet().TableData)
        {
            hasFreeSkin |= skin.Price.AsPrimitive() == 0;
        }

        if (!hasFreeSkin)
        {
            validator.Fail("expected a skin that costs nothing, found none.");
        }
    }
}
