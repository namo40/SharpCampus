using SharpCampus.Shared.Identity;

namespace SharpCampus.ApiServer.Matchmaking;

public interface IMatchQueue
{
    // Returns false when the account was already waiting, which makes a repeated enqueue a no-op.
    Task<bool> EnqueueAsync(UserId userId);

    Task<bool> ContainsAsync(UserId userId);

    // Also how a pairing is confirmed: both players leave the queue once their tickets exist.
    Task RemoveAsync(UserId userId);

    // Reads the two longest-waiting accounts without taking them out, so a player stays visibly queued
    // until the ticket that replaces that answer has been stored. Null when fewer than two are waiting.
    Task<(UserId First, UserId Second)?> PeekPairAsync();
}
