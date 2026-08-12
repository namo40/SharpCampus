using System.Text.Json;
using ConsoleAppFramework;
using SharpCampus.Shared.MasterData.Import;

namespace SharpCampus.MasterDataTool.Commands;

[RegisterCommands]
internal sealed class MasterDataCommands
{
    private const string DefaultInputDirectory = "masterdata";
    private const string DefaultOutputFile = "masterdata/masterdata.bin";

    /// <summary>Parses the master data sources and runs the schema validators without writing anything.</summary>
    /// <param name="input">-i, Directory holding the master data source files.</param>
    public int Validate(string input = DefaultInputDirectory)
    {
        if (!TryBuild(input, out var binary))
        {
            return 1;
        }

        Console.WriteLine($"Master data in {input} is valid ({binary.Length} bytes when built).");
        return 0;
    }

    /// <summary>Validates the master data sources and writes the database a deployed server loads.</summary>
    /// <param name="input">-i, Directory holding the master data source files.</param>
    /// <param name="output">-o, File to write the built database to.</param>
    public int Build(string input = DefaultInputDirectory, string output = DefaultOutputFile)
    {
        if (!TryBuild(input, out var binary))
        {
            return 1;
        }

        if (Path.GetDirectoryName(Path.GetFullPath(output)) is { } directory)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(output, binary);
        Console.WriteLine($"Wrote {binary.Length} bytes to {output}.");
        return 0;
    }

    private static bool TryBuild(string input, out byte[] binary)
    {
        binary = [];

        try
        {
            binary = MasterDataImporter.Build(input);
        }
        // JsonException already carries the file, line and position of the offending token.
        catch (Exception e) when (e is JsonException or FileNotFoundException)
        {
            Console.Error.WriteLine(e.Message);
            return false;
        }

        var result = MasterDataImporter.Validate(binary);
        if (!result.IsValidationFailed)
        {
            return true;
        }

        Console.Error.WriteLine(result.FormatFailedResults());
        return false;
    }
}
