namespace SharpCampus.GameCore;

public readonly record struct DuelTickResult(
    int TickNumber,
    IReadOnlyList<TickEvent> Events,
    bool Finished,
    DuelOutcome Outcome);
