using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.MasterData.Import;

namespace SharpCampus.Server.Common.MasterData;

public static class MasterDataLoader
{
    public static MemoryDatabase Load(string path)
    {
        // Both candidates live next to the assembly: the source file is linked into the output during
        // development, and the container image carries the prebuilt binary there instead.
        var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));

        if (Directory.Exists(fullPath))
        {
            return Import(fullPath);
        }

        if (File.Exists(fullPath))
        {
            // No validation here: whoever built the binary already ran it.
            return new MemoryDatabase(File.ReadAllBytes(fullPath));
        }

        // Starting without balance data would only surface as a crash on the first match, so the
        // servers refuse to start instead.
        throw new FileNotFoundException(
            $"Master data was not found at '{fullPath}'. Expected either the source directory copied into the "
            + "application output, or a prebuilt database file. Build one with: "
            + "dotnet run --project tools/SharpCampus.MasterDataTool -- build",
            fullPath);
    }

    private static MemoryDatabase Import(string directory)
    {
        var database = new MemoryDatabase(MasterDataImporter.Build(directory));

        var result = database.Validate();
        if (result.IsValidationFailed)
        {
            throw new InvalidOperationException(
                $"Master data in '{directory}' is invalid.{Environment.NewLine}{result.FormatFailedResults()}");
        }

        return database;
    }
}
