using MessagePack;

namespace SharpCampus.Shared.Internal.Bots;

/// <summary>
/// Whether the bot server had a bot to send.
/// </summary>
public enum SummonBotOutcome : byte
{
    /// <summary>A bot is signing in and heading for the queue.</summary>
    Deployed = 0,

    /// <summary>Every account in the bot server's pool is already playing.</summary>
    Exhausted = 1,
}

/// <summary>
/// Answer to a bot summon request.
/// </summary>
/// <param name="Outcome">Whether a bot was deployed, and why not when it was not.</param>
[MessagePackObject]
public sealed record SummonBotResult([property: Key(0)] SummonBotOutcome Outcome)
{
    /// <summary>The answer for a bot that is on its way to the queue.</summary>
    public static SummonBotResult Deployed { get; } = new(SummonBotOutcome.Deployed);

    /// <summary>The answer for a pool with no free account.</summary>
    public static SummonBotResult Exhausted { get; } = new(SummonBotOutcome.Exhausted);
}
