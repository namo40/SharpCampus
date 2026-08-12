namespace SharpCampus.Server.Common.Configuration;

public sealed class MasterDataOptions
{
    public const string SectionName = "MasterData";

    // Relative paths resolve against the application output directory. A directory is imported from the
    // source files, a file is loaded as a prebuilt database. Production note: a live service swaps master
    // data without a restart, by watching this path and rebuilding the database in place.
    public string Path { get; set; } = "masterdata";
}
