using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.MasterData.Import;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class MasterDataOptionsTests
{
    // The sources are linked into the output of everything that references Server.Common, this suite included.
    private static string LinkedSources { get; } = Path.Combine(AppContext.BaseDirectory, "masterdata");

    [Fact]
    public void AddMasterData_BindsConfiguredSectionAndImportsASourceDirectory()
    {
        var input = CopySourcesToTemporaryDirectory();
        var services = new ServiceCollection();

        services.AddMasterData(Configuration(input));

        using var provider = services.BuildServiceProvider();

        Assert.Equal(input, provider.GetRequiredService<IOptions<MasterDataOptions>>().Value.Path);
        Assert.Equal(20, provider.GetRequiredService<MemoryDatabase>().GameConfigTable.All[0].TickRate);
    }

    [Fact]
    public void AddMasterData_ResolvesARelativePathAgainstTheApplicationOutput()
    {
        var services = new ServiceCollection();

        services.AddMasterData(Configuration("masterdata"));

        using var provider = services.BuildServiceProvider();
        var database = provider.GetRequiredService<MemoryDatabase>();

        Assert.Equal(database.GameConfigTable.All[0].MaxLevel, database.GravityCurveTable.Count);
    }

    [Fact]
    public void AddMasterData_LoadsAPrebuiltDatabaseFile()
    {
        var file = Path.Combine(CreateTemporaryDirectory(), "masterdata.bin");
        File.WriteAllBytes(file, MasterDataImporter.Build(LinkedSources));
        var services = new ServiceCollection();

        services.AddMasterData(Configuration(file));

        using var provider = services.BuildServiceProvider();

        Assert.Equal(20, provider.GetRequiredService<MemoryDatabase>().GameConfigTable.All[0].TickRate);
    }

    [Fact]
    public void AddMasterData_FailsFastAndNamesTheToolWhenNothingIsThere()
    {
        var services = new ServiceCollection();

        var error = Assert.Throws<FileNotFoundException>(
            () => services.AddMasterData(Configuration(Path.Combine(CreateTemporaryDirectory(), "absent"))));

        Assert.Contains("SharpCampus.MasterDataTool", error.Message);
        Assert.Contains("build", error.Message);
    }

    [Fact]
    public void AddMasterData_FailsFastWhenTheImportedSourcesAreInvalid()
    {
        var input = CopySourcesToTemporaryDirectory();
        var source = Path.Combine(input, MasterDataImporter.GravityCurveFile);
        File.WriteAllLines(
            source,
            File.ReadAllLines(source).Where(line => !line.Contains("\"level\": 13", StringComparison.Ordinal)));
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() => services.AddMasterData(Configuration(input)));

        Assert.Contains("level 13 is missing", error.Message);
    }

    private static IConfiguration Configuration(string path) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MasterData:Path"] = path })
            .Build();

    private static string CopySourcesToTemporaryDirectory()
    {
        var target = CreateTemporaryDirectory();
        foreach (var source in Directory.EnumerateFiles(LinkedSources, "*.json"))
        {
            File.Copy(source, Path.Combine(target, Path.GetFileName(source)));
        }

        return target;
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sharpcampus-masterdata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
