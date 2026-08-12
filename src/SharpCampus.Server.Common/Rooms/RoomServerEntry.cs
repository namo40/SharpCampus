namespace SharpCampus.Server.Common.Rooms;

// One room server as the registry knows it. Written by that server's heartbeat, read by the pairing worker.
public sealed record RoomServerEntry(
    string Name,
    string ClientEndpoint,
    string ControlEndpoint,
    int RoomCount,
    int Capacity);
