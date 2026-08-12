using MessagePack;

namespace SharpCampus.Shared.Dtos;

/// <summary>
/// Answer to every matchmaking call.
/// </summary>
/// <param name="State">Where the caller stands.</param>
/// <param name="Ticket">Entry details, present only when <paramref name="State"/> is <see cref="MatchQueueState.Matched"/>.</param>
[MessagePackObject]
public sealed record MatchStatusResponse(
    [property: Key(0)] MatchQueueState State,
    [property: Key(1)] MatchTicket? Ticket)
{
    /// <summary>The answer for an account that is neither queued nor matched.</summary>
    public static MatchStatusResponse Idle { get; } = new(MatchQueueState.None, null);

    /// <summary>The answer for an account that is waiting for an opponent.</summary>
    public static MatchStatusResponse Waiting { get; } = new(MatchQueueState.Queued, null);
}
