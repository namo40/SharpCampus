namespace SharpCampus.RoomServer.Configuration;

public sealed class RoomServerOptions
{
    public const string SectionName = "RoomServer";

    // Identifies this instance in the room registry. Defaults to the shared server name.
    public string Name { get; set; } = "";

    // What a matched client is told to connect to.
    // Production note: in the deployed shape this is the Envoy address, and the room-id header routes
    // the connection to the pod that owns the room.
    public string ClientEndpoint { get; set; } = "";

    // What the ApiServer calls to create rooms here. Never exposed outside the cluster.
    public string ControlEndpoint { get; set; } = "";

    public int Capacity { get; set; } = 100;
}
