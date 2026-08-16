namespace SharpCampus.Client.Common;

public static class ClientEndpoints
{
    // In the deployed shape every call goes through the single entry point, so where the servers are is
    // something the environment says rather than something this build carries.
    public static string ApiServer { get; } =
        Environment.GetEnvironmentVariable("SHARPCAMPUS_SERVER") ?? "http://localhost:5001";
}
