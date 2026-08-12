using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;

namespace SharpCampus.ApiServer.Matchmaking;

public interface ITicketStore
{
    // A stored ticket is what "matched" means: it outlives the pairing worker and any one ApiServer instance.
    Task<MatchTicket?> GetAsync(UserId userId);

    Task StoreAsync(UserId userId, MatchTicket ticket);
}
