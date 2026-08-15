using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Settlement;

// Everything a finished game owes the settlement, and nothing about the room it came from: this layer
// is shared with the ApiServer and knows nothing about rooms or the events they publish.
public sealed record MatchSettlementRequest(
    MatchId MatchId,
    DuelOutcome Outcome,
    MatchEndReason Reason,
    int DurationTicks,
    MatchSettlementPlayer Player1,
    MatchSettlementPlayer Player2);

public sealed record MatchSettlementPlayer(UserId UserId, BoardStats Stats);
