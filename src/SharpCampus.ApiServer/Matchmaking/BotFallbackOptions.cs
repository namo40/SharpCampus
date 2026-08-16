namespace SharpCampus.ApiServer.Matchmaking;

public sealed class BotFallbackOptions
{
    public const string SectionName = "BotFallback";

    // What the ApiServer calls to send a bot into the queue. Never exposed outside the cluster.
    public string ControlEndpoint { get; set; } = "";

    // How long the last account in the queue waits before a bot is sent for.
    public int SummonAfterSeconds { get; set; } = 120;
}
