using System.Diagnostics.Metrics;
using SharpCampus.BotServer.Bots;

namespace SharpCampus.BotServer.Observability;

public sealed class BotMetrics
{
    public const string MeterName = "SharpCampus.BotServer";

    public const string ActiveBotsName = "sharpcampus.bots.active";

    // A slot is held for as long as its bot is in a match, so the leased accounts are the playing bots.
    public BotMetrics(IMeterFactory meters, BotAccountPool accounts)
        => meters.Create(MeterName).CreateObservableGauge(ActiveBotsName, () => accounts.LeasedCount);
}
