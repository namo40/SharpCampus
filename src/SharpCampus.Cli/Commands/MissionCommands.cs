using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Cli.Resources;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Services;
using SharpCampus.Shared.Values;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class MissionCommands
{
    private const string ApiServerAddress = "http://localhost:5001";
    private const string ApiServerLabel = "ApiServer";

    /// <summary>Lists today's missions with what you have done towards them.</summary>
    [Command("missions")]
    public async Task ListAsync()
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        using var channel = GrpcChannel.ForAddress(ApiServerAddress);
        var client = Create(channel, session);

        try
        {
            var missions = await client.GetMissionsAsync();

            AnsiConsole.MarkupLineInterpolated($"[grey]{Strings.MissionsHeader}[/]");

            var table = new Table();
            table.AddColumn(Strings.MissionColumnMission);
            table.AddColumn(Strings.MissionColumnProgress);
            table.AddColumn(Strings.MissionColumnReward);
            table.AddColumn(Strings.MissionColumnStatus);

            foreach (var item in missions)
            {
                table.AddRow(
                    Name(item),
                    new Markup(Progress(item)),
                    new Markup($"{item.Mission.RewardCoins.AsPrimitive()}"),
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
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, ApiServerLabel, ApiServerAddress)}[/]");
        }
    }

    /// <summary>Takes the coins a mission you finished today pays.</summary>
    /// <param name="missionId">Identifier of the mission, as the missions command lists it.</param>
    [Command("claim")]
    public async Task ClaimAsync([Argument] string missionId)
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLineInterpolated($"[yellow]{Strings.NotLoggedIn}[/]");
            return;
        }

        using var channel = GrpcChannel.ForAddress(ApiServerAddress);
        var client = Create(channel, session);
        var mission = Normalize(missionId);

        try
        {
            switch (await client.ClaimAsync(mission))
            {
                case MissionClaimResult.Claimed:
                    AnsiConsole.MarkupLineInterpolated($"[green]{Localization.Format(Strings.MissionClaimed, Strings.MissionName(mission))}[/]");
                    break;
                case MissionClaimResult.NotCompleted:
                    AnsiConsole.MarkupLineInterpolated($"[yellow]{Localization.Format(Strings.MissionNotCompleted, Strings.MissionName(mission))}[/]");
                    break;
                case MissionClaimResult.AlreadyClaimed:
                    AnsiConsole.MarkupLineInterpolated($"[yellow]{Localization.Format(Strings.MissionAlreadyClaimed, Strings.MissionName(mission))}[/]");
                    break;
                case MissionClaimResult.UnknownMission:
                    AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.MissionUnknown, mission.AsPrimitive())}[/]");
                    break;
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Strings.SessionExpired}[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, ApiServerLabel, ApiServerAddress)}[/]");
        }
    }

    // Master data keys are upper case, so `claim win_1` asks for the same mission as `claim WIN_1`.
    private static MissionId Normalize(string missionId) => new(missionId.ToUpperInvariant());

    private static IMissionService Create(GrpcChannel channel, ClientSession session) =>
        MagicOnionClient.Create<IMissionService>(channel)
            .WithHeaders(new Metadata { { "authorization", $"Bearer {session.AccessToken}" } });

    // The identifier is what claim takes, so it travels beside the localized name instead of being a
    // column of its own.
    private static Markup Name(MissionItem item) =>
        new($"{Markup.Escape(Strings.MissionName(item.Mission.NameKey))} [grey]{item.Mission.MissionId.AsPrimitive()}[/]");

    // Progress runs past the goal in the row, but a mission is never more than finished.
    private static string Progress(MissionItem item) =>
        $"{Math.Min(item.Progress, item.Mission.Goal)}/{item.Mission.Goal}";

    private static string Status(MissionItem item) => item switch
    {
        { Claimed: true } => $"[grey]{Strings.MissionStatusClaimed}[/]",
        { Progress: var progress, Mission.Goal: var goal } when progress >= goal =>
            $"[green]{Strings.MissionStatusClaimable}[/]",
        _ => Strings.MissionStatusInProgress,
    };
}
