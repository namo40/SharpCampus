using SharpCampus.Shared.Identity;

namespace SharpCampus.Server.Common.Matchmaking;

public interface IActiveRoomStore
{
    Task<ActiveRoom?> GetAsync(UserId userId);

    Task StoreAsync(UserId userId, ActiveRoom room);

    // Releasing takes the pairing ticket with it: both were written for the same match, and a ticket
    // that outlives its room is what turns the next queue attempt into a refused seat.
    Task ReleaseAsync(UserId userId);
}
