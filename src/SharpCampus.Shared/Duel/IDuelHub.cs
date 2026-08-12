using MagicOnion;
using SharpCampus.GameCore;

namespace SharpCampus.Shared.Duel;

/// <summary>
/// A seat in a duel room. The server owns the simulation: a client sends inputs and draws what it is told.
/// </summary>
public interface IDuelHub : IStreamingHub<IDuelHub, IDuelHubReceiver>
{
    /// <summary>
    /// Takes the seat matchmaking reserved for the caller. The room already exists and already knows who belongs in it.
    /// </summary>
    /// <param name="request">Room to enter and the entry token that proves the caller belongs there.</param>
    /// <returns>The seat that was assigned, or a rejection when the room cannot take the caller.</returns>
    Task<JoinRoomResult> JoinAsync(JoinRoomRequest request);

    /// <summary>
    /// Queues inputs for the next tick. Inputs beyond the per-tick allowance are discarded.
    /// </summary>
    /// <param name="inputs">Commands in the order the player made them.</param>
    /// <returns>A task that completes once the inputs are queued.</returns>
    Task SendInputsAsync(GameInput[] inputs);

    /// <summary>
    /// Reads both boards in full, for entry or after a gap in the delta stream.
    /// </summary>
    /// <returns>Both boards as of the tick the room answered on.</returns>
    Task<DuelSnapshot> RequestSnapshotAsync();

    /// <summary>
    /// Gives up the match. The opponent wins immediately.
    /// </summary>
    /// <returns>A task that completes once the forfeit is queued.</returns>
    Task ForfeitAsync();
}
