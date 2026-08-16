using System.Resources;

namespace SharpCampus.Client.Resources;

// What the menus say in their own right; everything a client of any shape needs is in Client.Common.
// Hand written rather than generated: MSBuild puts the strongly typed output in the intermediate
// folder, where the IDE's project model does not pick it up. Member names are the resource keys.
internal static class Strings
{
    private static readonly ResourceManager _resources =
        new("SharpCampus.Client.Resources.Strings", typeof(Strings).Assembly);

    public static string AccountEmailLabel => Get(nameof(AccountEmailLabel));
    public static string AccountTitle => Get(nameof(AccountTitle));
    public static string AccountUserIdLabel => Get(nameof(AccountUserIdLabel));
    public static string AppTagline => Get(nameof(AppTagline));
    public static string DuelTitle => Get(nameof(DuelTitle));
    public static string GuestHint => Get(nameof(GuestHint));
    public static string InteractiveTerminalRequired => Get(nameof(InteractiveTerminalRequired));
    public static string LobbyTitle => Get(nameof(LobbyTitle));
    public static string MenuAccount => Get(nameof(MenuAccount));
    public static string MenuBack => Get(nameof(MenuBack));
    public static string MenuBuy => Get(nameof(MenuBuy));
    public static string MenuChangeNickname => Get(nameof(MenuChangeNickname));
    public static string MenuDuel => Get(nameof(MenuDuel));
    public static string MenuEquip => Get(nameof(MenuEquip));
    public static string MenuExit => Get(nameof(MenuExit));
    public static string MenuHistory => Get(nameof(MenuHistory));
    public static string MenuLinkAccount => Get(nameof(MenuLinkAccount));
    public static string MenuLogIn => Get(nameof(MenuLogIn));
    public static string MenuLogOut => Get(nameof(MenuLogOut));
    public static string MenuMissions => Get(nameof(MenuMissions));
    public static string MenuPlayAsGuest => Get(nameof(MenuPlayAsGuest));
    public static string MenuPrompt => Get(nameof(MenuPrompt));
    public static string MenuRankings => Get(nameof(MenuRankings));
    public static string MenuRetry => Get(nameof(MenuRetry));
    public static string MenuShop => Get(nameof(MenuShop));
    public static string MenuSignUp => Get(nameof(MenuSignUp));
    public static string MissionSelect => Get(nameof(MissionSelect));
    public static string MissionsTitle => Get(nameof(MissionsTitle));
    public static string PressAnyKey => Get(nameof(PressAnyKey));
    public static string PromptEmail => Get(nameof(PromptEmail));
    public static string PromptNickname => Get(nameof(PromptNickname));
    public static string PromptPassword => Get(nameof(PromptPassword));
    public static string QueueCancelHint => Get(nameof(QueueCancelHint));
    public static string RankSelectBoard => Get(nameof(RankSelectBoard));
    public static string RankingsTitle => Get(nameof(RankingsTitle));
    public static string SessionEndedNotice => Get(nameof(SessionEndedNotice));
    public static string ShopActionTitle => Get(nameof(ShopActionTitle));
    public static string ShopSelectSkin => Get(nameof(ShopSelectSkin));
    public static string ShopTitle => Get(nameof(ShopTitle));
    public static string ShopWearing => Get(nameof(ShopWearing));
    public static string TitleMenuTitle => Get(nameof(TitleMenuTitle));

    private static string Get(string name) => _resources.GetString(name)!;
}
