using System.Text.Json;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.MasterData.Import;
using SharpCampus.Shared.Missions;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.Shared.Tests;

public sealed class MasterDataImporterTests
{
    [Fact]
    public void Build_AcceptsTheSourcesInTheRepository()
    {
        var result = MasterDataImporter.Validate(MasterDataImporter.Build(RepositorySources.Directory));

        Assert.False(result.IsValidationFailed, result.FormatFailedResults());
    }

    [Fact]
    public void Build_SurvivesARoundTripThroughTheBinary()
    {
        var database = new MemoryDatabase(MasterDataImporter.Build(RepositorySources.Directory));

        Assert.Equal(20, database.GameConfigTable.FindById(GameConfig.SingleRowId).TickRate);
        Assert.Equal(15, database.GameConfigTable.FindById(GameConfig.SingleRowId).MaxLevel);
        Assert.Equal(20, database.GravityCurveTable.FindByLevel(1).TicksPerCell);
        Assert.Equal(1, database.GravityCurveTable.FindByLevel(15).TicksPerCell);
        Assert.Equal(4, database.AttackTableTable.FindByLinesCleared(4).Garbage);
        Assert.Equal(3, database.ComboTableTable.FindByComboMin(6).Bonus);
        Assert.Equal(1000, database.EconomyTable.FindById(GameConfig.SingleRowId).RatingInitial.AsPrimitive());
    }

    [Fact]
    public void Build_KeepsValueObjectKeysAndEnumsAcrossTheBinary()
    {
        var database = new MemoryDatabase(MasterDataImporter.Build(RepositorySources.Directory));

        var classic = database.SkinTable.FindBySkinId(new SkinId("CLASSIC"));
        Assert.Equal(0, classic.Price.AsPrimitive());
        Assert.Equal("skin.classic.name", classic.NameKey);

        var mission = database.MissionTable.FindByMissionId(new MissionId("SEND_20"));
        Assert.Equal(MissionMetric.GarbageSent, mission.Metric);
        Assert.Equal(20, mission.Goal);
    }

    [Fact]
    public void Build_NamesTheFileAndLineOfAMalformedValue()
    {
        var directory = Rewrite(
            MasterDataImporter.GravityCurveFile,
            json => json.Replace("\"ticksPerCell\": 20", "\"ticksPerCell\": \"fast\""));

        var error = Assert.Throws<JsonException>(() => MasterDataImporter.Build(directory));

        Assert.Contains(MasterDataImporter.GravityCurveFile, error.Message, StringComparison.Ordinal);
        Assert.Contains("ticksPerCell", error.Message, StringComparison.Ordinal);
        Assert.Contains("LineNumber", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NamesTheFileAndLineOfBrokenSyntax()
    {
        var directory = Rewrite(
            MasterDataImporter.GravityCurveFile,
            json => json.Replace("\"level\": 1,", "\"level\" 1,"));

        var error = Assert.Throws<JsonException>(() => MasterDataImporter.Build(directory));

        Assert.Contains(MasterDataImporter.GravityCurveFile, error.Message, StringComparison.Ordinal);
        Assert.Contains("LineNumber", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ReportsAMissingTableFile()
    {
        var directory = RepositorySources.CopyToTemporaryDirectory();
        File.Delete(Path.Combine(directory, MasterDataImporter.EconomyFile));

        var error = Assert.Throws<FileNotFoundException>(() => MasterDataImporter.Build(directory));

        Assert.Contains(MasterDataImporter.EconomyFile, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RejectsAnEmptyTableFile()
    {
        var directory = Rewrite(MasterDataImporter.MissionsFile, _ => "[]");

        var error = Assert.Throws<JsonException>(() => MasterDataImporter.Build(directory));

        Assert.Contains(MasterDataImporter.MissionsFile, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ReportsAnAbsentSourceDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sharpcampus-masterdata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        var error = Assert.Throws<FileNotFoundException>(() => MasterDataImporter.Build(directory));

        Assert.Contains(MasterDataImporter.GameConfigFile, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsAGapInTheGravityCurve()
    {
        var directory = RepositorySources.CopyToTemporaryDirectory();
        var path = Path.Combine(directory, MasterDataImporter.GravityCurveFile);
        File.WriteAllLines(
            path,
            File.ReadAllLines(path).Where(line => !line.Contains("\"level\": 13", StringComparison.Ordinal)));

        var result = MasterDataImporter.Validate(MasterDataImporter.Build(directory));

        Assert.True(result.IsValidationFailed);
        Assert.Contains("level 13 is missing", result.FormatFailedResults());
    }

    private static string Rewrite(string fileName, Func<string, string> edit)
    {
        var directory = RepositorySources.CopyToTemporaryDirectory();
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, edit(File.ReadAllText(path)));
        return directory;
    }
}
