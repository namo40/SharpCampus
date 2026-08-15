using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;

namespace SharpCampus.Server.Common.Settlement;

// What the write has to make true, already decided: the two profile columns to set and the row to
// insert for each player.
public sealed record MatchSettlement(SettledPlayer Player1, SettledPlayer Player2);

public sealed record SettledPlayer(UserId UserId, Coins Coins, Rating Rating, MatchRecord Record);
