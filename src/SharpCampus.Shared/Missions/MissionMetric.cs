namespace SharpCampus.Shared.Missions;

/// <summary>
/// The player statistic a daily mission counts towards its goal.
/// </summary>
public enum MissionMetric
{
    /// <summary>Matches the player finished, win or lose.</summary>
    MatchesPlayed = 0,

    /// <summary>Matches the player won.</summary>
    Wins = 1,

    /// <summary>Rows the player cleared.</summary>
    LinesCleared = 2,

    /// <summary>Garbage rows the player sent to the opponent.</summary>
    GarbageSent = 3,

    /// <summary>Hard drops the player performed.</summary>
    HardDrops = 4,
}
