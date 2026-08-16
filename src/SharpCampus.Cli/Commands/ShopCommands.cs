using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Cli.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Services;
using SharpCampus.Shared.Values;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class ShopCommands
{
    private const string ApiServerLabel = "ApiServer";

    /// <summary>Lists every skin with its price and whether you own it.</summary>
    [Command("skins")]
    public async Task ListAsync()
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        using var channel = GrpcChannel.ForAddress(ClientEndpoints.ApiServer);
        var client = Create(channel, session);

        try
        {
            var shop = await client.GetShopAsync();

            AnsiConsole.MarkupLineInterpolated(
                $"[grey]{Localization.Format(Strings.ShopBalance, shop.Balance.AsPrimitive())}[/]");

            var table = new Table();
            table.AddColumn(Strings.ShopColumnSkin);
            table.AddColumn(Strings.ShopColumnPrice);
            table.AddColumn(string.Empty);
            table.AddColumn(Strings.ShopColumnStatus);

            foreach (var item in shop.Items.OrderBy(item => item.Skin.Price.AsPrimitive()))
            {
                table.AddRow(
                    Name(item.Skin),
                    new Markup($"{item.Skin.Price.AsPrimitive()}"),
                    Sample(item.Skin),
                    new Markup(Status(item)));
            }

            AnsiConsole.Write(table);
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Strings.SessionExpired}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, ApiServerLabel, ClientEndpoints.ApiServer)}[/]");
        }
    }

    /// <summary>Buys a skin with the coins matches have paid out.</summary>
    /// <param name="skinId">Identifier of the skin, as the skins command lists it.</param>
    [Command("buy")]
    public async Task BuyAsync([Argument] string skinId)
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        using var channel = GrpcChannel.ForAddress(ClientEndpoints.ApiServer);
        var client = Create(channel, session);
        var skin = Normalize(skinId);

        try
        {
            switch (await client.PurchaseAsync(skin))
            {
                case SkinPurchaseResult.Purchased:
                    AnsiConsole.MarkupLineInterpolated($"[green]{Localization.Format(Strings.ShopPurchased, Strings.SkinName(skin))}[/]");
                    break;
                case SkinPurchaseResult.AlreadyOwned:
                    AnsiConsole.MarkupLineInterpolated($"[yellow]{Localization.Format(Strings.ShopAlreadyOwned, Strings.SkinName(skin))}[/]");
                    break;
                case SkinPurchaseResult.InsufficientCoins:
                    AnsiConsole.MarkupLineInterpolated($"[red]{Strings.ShopInsufficientCoins}[/]");
                    break;
                case SkinPurchaseResult.UnknownSkin:
                    AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ShopUnknownSkin, skin.AsPrimitive())}[/]");
                    break;
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Strings.SessionExpired}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, ApiServerLabel, ClientEndpoints.ApiServer)}[/]");
        }
    }

    /// <summary>Equips a skin you own; both screens of your next match draw your board with it.</summary>
    /// <param name="skinId">Identifier of the skin, as the skins command lists it.</param>
    [Command("equip")]
    public async Task EquipAsync([Argument] string skinId)
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        using var channel = GrpcChannel.ForAddress(ClientEndpoints.ApiServer);
        var client = Create(channel, session);
        var skin = Normalize(skinId);

        try
        {
            switch (await client.EquipAsync(skin))
            {
                case SkinEquipResult.Equipped:
                    AnsiConsole.MarkupLineInterpolated($"[green]{Localization.Format(Strings.ShopEquipped, Strings.SkinName(skin))}[/]");
                    break;
                case SkinEquipResult.NotOwned:
                    AnsiConsole.MarkupLineInterpolated($"[yellow]{Localization.Format(Strings.ShopNotOwned, Strings.SkinName(skin))}[/]");
                    break;
                case SkinEquipResult.UnknownSkin:
                    AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ShopUnknownSkin, skin.AsPrimitive())}[/]");
                    break;
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Strings.SessionExpired}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, ApiServerLabel, ClientEndpoints.ApiServer)}[/]");
        }
    }

    // Master data keys are upper case, so `buy mono` asks for the same skin as `buy MONO`.
    private static SkinId Normalize(string skinId) => new(skinId.ToUpperInvariant());

    private static IShopService Create(GrpcChannel channel, ClientSession session) =>
        MagicOnionClient.Create<IShopService>(channel)
            .WithHeaders(new Metadata { { "authorization", $"Bearer {session.AccessToken}" } });

    // The identifier is what buy and equip take, so it travels beside the localized name instead of
    // being a column of its own.
    private static Markup Name(Skin skin) =>
        new($"{Markup.Escape(Strings.SkinName(skin.NameKey))} [grey]{skin.SkinId.AsPrimitive()}[/]");

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
        { Equipped: true } => $"[green]{Strings.ShopStatusEquipped}[/]",
        { Owned: true } => Strings.ShopStatusOwned,
        _ => string.Empty,
    };
}
