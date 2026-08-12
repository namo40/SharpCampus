using SharpCampus.GameCore;

namespace SharpCampus.RoomServer.MasterData;

// Everything a room needs from master data, already converted to the units the loop counts in.
public sealed record DuelRules(
    SimulationConfig Simulation,
    int TickRate,
    int InputPerTickMax,
    int NextCount,
    int CountdownTicks,
    int JoinTimeoutTicks);
