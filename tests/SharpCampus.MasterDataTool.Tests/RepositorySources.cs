using SharpCampus.Shared.MasterData.Import;

namespace SharpCampus.MasterDataTool.Tests;

// The commands run against copies of the checked-in sources, so a test that breaks a file leaves
// the working tree alone.
internal static class RepositorySources
{
    public static string CopyToTemporaryDirectory()
    {
        var target = Path.Combine(Path.GetTempPath(), $"sharpcampus-masterdata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(target);
        foreach (var source in Directory.EnumerateFiles(Locate(), "*.json"))
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
