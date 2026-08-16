using SharpCampus.Client.Common;
using SharpCampus.Client.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Values;
using Spectre.Console;
using Spectre.Console.Rendering;
using CommonStrings = SharpCampus.Client.Common.Resources.Strings;

namespace SharpCampus.Client.Screens;

// The catalog is the menu: a skin is picked from the same table that shows what it costs and whether
// it is already yours, and only the actions that skin actually has are offered.
internal sealed class ShopScreen(ApiGateway gateway) : Screen(gateway)
{
    protected override async Task<ScreenResult> ShowAsync()
    {
        while (true)
        {
            Ui.Begin(Strings.ShopTitle);

            var shop = await Gateway.Shop.GetShopAsync();
            var items = shop.Items.OrderBy(entry => entry.Skin.Price.AsPrimitive()).ToArray();

            Ui.Hint(Localization.Format(CommonStrings.ShopBalance, shop.Balance.AsPrimitive()));
            AnsiConsole.Write(BuildTable(items));

            if (Ui.Choose(Strings.ShopSelectSkin, Choices(items), choice => choice.Label).Value is not { } item)
            {
                return ScreenResult.Back;
            }

            await ActAsync(item);
        }
    }

    private async Task ActAsync(ShopItem item)
    {
        var name = CommonStrings.SkinName(item.Skin.NameKey);
        var actions = Actions(item);

        if (actions.Count == 0)
        {
            Ui.Warn(Localization.Format(Strings.ShopWearing, name));
            Ui.Pause();
            return;
        }

        switch (Ui.Choose(Localization.Format(Strings.ShopActionTitle, name), actions, Label))
        {
            case ShopAction.Buy:
                Report(await Gateway.Shop.PurchaseAsync(item.Skin.SkinId), name, item.Skin.SkinId);
                break;

            case ShopAction.Equip:
                Report(await Gateway.Shop.EquipAsync(item.Skin.SkinId), name, item.Skin.SkinId);
                break;

            case ShopAction.Back:
                return;
        }

        Ui.Pause();
    }

    private static IReadOnlyList<ShopAction> Actions(ShopItem item)
    {
        List<ShopAction> actions = [];

        if (!item.Owned)
        {
            actions.Add(ShopAction.Buy);
        }

        if (item is { Owned: true, Equipped: false })
        {
            actions.Add(ShopAction.Equip);
        }

        if (actions.Count > 0)
        {
            actions.Add(ShopAction.Back);
        }

        return actions;
    }

    private static void Report(SkinPurchaseResult result, string name, SkinId skinId)
    {
        switch (result)
        {
            case SkinPurchaseResult.Purchased:
                Ui.Info(Localization.Format(CommonStrings.ShopPurchased, name));
                break;
            case SkinPurchaseResult.AlreadyOwned:
                Ui.Warn(Localization.Format(CommonStrings.ShopAlreadyOwned, name));
                break;
            case SkinPurchaseResult.InsufficientCoins:
                Ui.Error(CommonStrings.ShopInsufficientCoins);
                break;
            case SkinPurchaseResult.UnknownSkin:
                Ui.Error(Localization.Format(CommonStrings.ShopUnknownSkin, skinId.AsPrimitive()));
                break;
        }
    }

    private static void Report(SkinEquipResult result, string name, SkinId skinId)
    {
        switch (result)
        {
            case SkinEquipResult.Equipped:
                Ui.Info(Localization.Format(CommonStrings.ShopEquipped, name));
                break;
            case SkinEquipResult.NotOwned:
                Ui.Warn(Localization.Format(CommonStrings.ShopNotOwned, name));
                break;
            case SkinEquipResult.UnknownSkin:
                Ui.Error(Localization.Format(CommonStrings.ShopUnknownSkin, skinId.AsPrimitive()));
                break;
        }
    }

    private static IReadOnlyList<Choice<ShopItem?>> Choices(IReadOnlyList<ShopItem> items) =>
    [
        .. items.Select(item => new Choice<ShopItem?>(CommonStrings.SkinName(item.Skin.NameKey), item)),
        new Choice<ShopItem?>(Strings.MenuBack, null),
    ];

    private static Table BuildTable(IReadOnlyList<ShopItem> items)
    {
        var table = new Table();
        table.AddColumn(CommonStrings.ShopColumnSkin);
        table.AddColumn(CommonStrings.ShopColumnPrice);
        table.AddColumn(string.Empty);
        table.AddColumn(CommonStrings.ShopColumnStatus);

        foreach (var item in items)
        {
            table.AddRow(
                new Markup(Markup.Escape(CommonStrings.SkinName(item.Skin.NameKey))),
                new Markup($"{item.Skin.Price.AsPrimitive()}"),
                Sample(item.Skin),
                new Markup(Status(item)));
        }

        return table;
    }

    // Built as a paragraph rather than markup: a glyph like [] would otherwise read as a markup tag.
    private static IRenderable Sample(Skin skin)
    {
        var sample = new Paragraph();
        foreach (var color in skin.PaletteColors())
        {
            sample.Append(skin.BlockGlyph, new Style(Color.FromConsoleColor(Enum.Parse<ConsoleColor>(color))));
        }

        return sample;
    }

    private static string Status(ShopItem item) => item switch
    {
        { Equipped: true } => $"[green]{CommonStrings.ShopStatusEquipped}[/]",
        { Owned: true } => CommonStrings.ShopStatusOwned,
        _ => string.Empty,
    };

    private static string Label(ShopAction action) => action switch
    {
        ShopAction.Buy => Strings.MenuBuy,
        ShopAction.Equip => Strings.MenuEquip,
        _ => Strings.MenuBack,
    };

    private enum ShopAction
    {
        Buy,
        Equip,
        Back,
    }
}
