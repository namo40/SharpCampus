using SharpCampus.Client.Common;
using SharpCampus.Client.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Values;
using Spectre.Console;
using CommonStrings = SharpCampus.Client.Common.Resources.Strings;

namespace SharpCampus.Client.Screens;

// Today's missions, and the one thing there is to do with them. Every mission is offered rather than
// only the finished ones: which of the three answers comes back is the server's to give.
internal sealed class MissionScreen(ApiGateway gateway) : Screen(gateway)
{
    protected override async Task<ScreenResult> ShowAsync()
    {
        while (true)
        {
            Ui.Begin(Strings.MissionsTitle);

            var missions = await Gateway.Missions.GetMissionsAsync();

            Ui.Hint(CommonStrings.MissionsHeader);
            AnsiConsole.Write(BuildTable(missions));

            if (Ui.Choose(Strings.MissionSelect, Choices(missions), choice => choice.Label).Value is not { } item)
            {
                return ScreenResult.Back;
            }

            var missionId = item.Mission.MissionId;
            Report(await Gateway.Missions.ClaimAsync(missionId), CommonStrings.MissionName(item.Mission.NameKey), missionId);
            Ui.Pause();
        }
    }

    private static void Report(MissionClaimResult result, string name, MissionId missionId)
    {
        switch (result)
        {
            case MissionClaimResult.Claimed:
                Ui.Info(Localization.Format(CommonStrings.MissionClaimed, name));
                break;
            case MissionClaimResult.NotCompleted:
                Ui.Warn(Localization.Format(CommonStrings.MissionNotCompleted, name));
                break;
            case MissionClaimResult.AlreadyClaimed:
                Ui.Warn(Localization.Format(CommonStrings.MissionAlreadyClaimed, name));
                break;
            case MissionClaimResult.UnknownMission:
                Ui.Error(Localization.Format(CommonStrings.MissionUnknown, missionId.AsPrimitive()));
                break;
        }
    }

    private static IReadOnlyList<Choice<MissionItem?>> Choices(IReadOnlyList<MissionItem> missions) =>
    [
        .. missions.Select(item => new Choice<MissionItem?>(CommonStrings.MissionName(item.Mission.NameKey), item)),
        new Choice<MissionItem?>(Strings.MenuBack, null),
    ];

    private static Table BuildTable(IReadOnlyList<MissionItem> missions)
    {
        var table = new Table();
        table.AddColumn(CommonStrings.MissionColumnMission);
        table.AddColumn(CommonStrings.MissionColumnProgress);
        table.AddColumn(CommonStrings.MissionColumnReward);
        table.AddColumn(CommonStrings.MissionColumnStatus);

        foreach (var item in missions)
        {
            table.AddRow(
                new Markup(Markup.Escape(CommonStrings.MissionName(item.Mission.NameKey))),
                new Markup(Progress(item)),
                new Markup($"{item.Mission.RewardCoins.AsPrimitive()}"),
                new Markup(Status(item)));
        }

        return table;
    }

    // Progress runs past the goal in the row, but a mission is never more than finished.
    private static string Progress(MissionItem item) =>
        $"{Math.Min(item.Progress, item.Mission.Goal)}/{item.Mission.Goal}";

    private static string Status(MissionItem item) => item switch
    {
        { Claimed: true } => $"[grey]{CommonStrings.MissionStatusClaimed}[/]",
        { Progress: var progress, Mission.Goal: var goal } when progress >= goal =>
            $"[green]{CommonStrings.MissionStatusClaimable}[/]",
        _ => CommonStrings.MissionStatusInProgress,
    };
}
