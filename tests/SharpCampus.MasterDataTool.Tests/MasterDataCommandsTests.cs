using SharpCampus.MasterDataTool.Commands;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.MasterData.Import;
using Xunit;

namespace SharpCampus.MasterDataTool.Tests;

public sealed class MasterDataCommandsTests
{
    [Fact]
    public void Validate_SucceedsOnTheCheckedInSources()
    {
        using var console = new ConsoleCapture();

        var exitCode = new MasterDataCommands().Validate(RepositorySources.CopyToTemporaryDirectory());

        Assert.Equal(0, exitCode);
        Assert.Contains("is valid", console.Output);
        Assert.Empty(console.Error);
    }

    [Fact]
    public void Validate_ReportsTheFileAndLineOfAMalformedSource()
    {
        var input = Rewrite(
            MasterDataImporter.GravityCurveFile,
            json => json.Replace("\"ticksPerCell\": 20", "\"ticksPerCell\": \"fast\""));
        using var console = new ConsoleCapture();

        var exitCode = new MasterDataCommands().Validate(input);

        Assert.Equal(1, exitCode);
        Assert.Contains(MasterDataImporter.GravityCurveFile, console.Error);
        Assert.Contains("ticksPerCell", console.Error);
        Assert.Contains("LineNumber", console.Error);
    }

    [Fact]
    public void Validate_ReportsAValidationFailure()
    {
        var input = RepositorySources.CopyToTemporaryDirectory();
        var path = Path.Combine(input, MasterDataImporter.GravityCurveFile);
        File.WriteAllLines(
            path,
            File.ReadAllLines(path).Where(line => !line.Contains("\"level\": 13", StringComparison.Ordinal)));
        using var console = new ConsoleCapture();

        var exitCode = new MasterDataCommands().Validate(input);

        Assert.Equal(1, exitCode);
        Assert.Contains("level 13 is missing", console.Error);
    }

    [Fact]
    public void Build_WritesADatabaseTheServersCanOpen()
    {
        var input = RepositorySources.CopyToTemporaryDirectory();
        var output = Path.Combine(input, "built", "masterdata.bin");
        using var console = new ConsoleCapture();

        var exitCode = new MasterDataCommands().Build(input, output);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(output));
        Assert.Equal(20, new MemoryDatabase(File.ReadAllBytes(output)).GameConfigTable.All[0].TickRate);
    }

    [Fact]
    public void Build_WritesNothingWhenASourceIsBroken()
    {
        var input = Rewrite(MasterDataImporter.EconomyFile, json => json.Replace("\"winCoins\":", "\"winCoins\""));
        var output = Path.Combine(input, "built", "masterdata.bin");
        using var console = new ConsoleCapture();

        var exitCode = new MasterDataCommands().Build(input, output);

        Assert.Equal(1, exitCode);
        Assert.False(File.Exists(output));
        Assert.NotEmpty(console.Error);
    }

    private static string Rewrite(string fileName, Func<string, string> edit)
    {
        var input = RepositorySources.CopyToTemporaryDirectory();
        var path = Path.Combine(input, fileName);
        File.WriteAllText(path, edit(File.ReadAllText(path)));
        return input;
    }

    // The commands report through Console, so the assertions need it redirected for the call.
    private sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter _previousOut = Console.Out;
        private readonly TextWriter _previousError = Console.Error;
        private readonly StringWriter _out = new();
        private readonly StringWriter _error = new();

        public ConsoleCapture()
        {
            Console.SetOut(_out);
            Console.SetError(_error);
        }

        public string Output => _out.ToString();

        public string Error => _error.ToString();

        public void Dispose()
        {
            Console.SetOut(_previousOut);
            Console.SetError(_previousError);
        }
    }
}
