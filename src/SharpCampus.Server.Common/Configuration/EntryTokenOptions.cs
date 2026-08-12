namespace SharpCampus.Server.Common.Configuration;

public sealed class EntryTokenOptions
{
    public const string SectionName = "EntryToken";

    // Both servers have to agree on this value: the ApiServer signs entry tokens with it and the
    // RoomServer verifies them.
    // Production note: inject this from a K8s Secret rather than a checked-in file, and rotate it.
    public string Secret { get; set; } = "";
}
