using SharpCampus.Shared.MasterData.Import;

namespace SharpCampus.Shared.Tests;

// The source files are checked in, so the round-trip tests run against the same data the servers ship with.
internal static class RepositorySources
{
    public static string Directory { get; } = Locate();

    public static string CopyToTemporaryDirectory()
    {
        var target = Path.Combine(Path.GetTempPath(), $"sharpcampus-masterdata-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(target);
        foreach (var source in System.IO.Directory.EnumerateFiles(Directory, "*.json"))
        {
            File.Copy(source, Path.Combine(target, Path.GetFileName(source)));
        }

        return target;
    }

    private static string Locate()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "masterdata");
            if (File.Exists(Path.Combine(candidate, MasterDataImporter.GameConfigFile)))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The master data source files were not found above the test assembly.");
    }
}
