using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.RoomServer.Rooms;

// Published once per finished game, rematches included. It carries its own copy of everything the
// settlement needs, so a handler that runs after the room has closed still has a whole match to work
// from.
public sealed record MatchFinishedEvent(
    MatchId MatchId,
    RoomId RoomId,
    DuelOutcome Outcome,
    MatchEndReason Reason,
    int DurationTicks,
    MatchParticipant Player1,
    MatchParticipant Player2);

public sealed record MatchParticipant(UserId UserId, BoardStats Stats);
