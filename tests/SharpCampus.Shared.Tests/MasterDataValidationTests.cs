using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.Shared.Tests;

public sealed class MasterDataValidationTests
{
    [Fact]
    public void Validate_AcceptsACompleteDataset()
    {
        var result = new MasterDataSample().ToDatabase().Validate();

        Assert.False(result.IsValidationFailed, result.FormatFailedResults());
    }

    [Fact]
    public void Validate_RejectsAGravityCurveThatStopsBelowTheMaximumLevel()
    {
        var sample = new MasterDataSample();
        sample.GravityCurve.RemoveAll(row => row.Level == 3);

        Assert.Contains("level 3 is missing", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsAGravityCurveThatNeverAdvances()
    {
        var sample = new MasterDataSample();
        sample.GravityCurve[1] = new GravityCurve { Level = 2, TicksPerCell = 0 };

        Assert.Contains("TicksPerCell", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsAnAttackTableThatDoesNotCoverEveryLineCount()
    {
        var sample = new MasterDataSample();
        sample.AttackTable.RemoveAll(row => row.LinesCleared == 3);

        Assert.Contains("expected 4 rows", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsOverlappingComboRanges()
    {
        var sample = new MasterDataSample();
        sample.ComboTable[1] = new ComboTable { ComboMin = 3, ComboMax = 5, Bonus = 2 };

        Assert.Contains("overlaps", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsAComboRangeThatEndsBeforeItStarts()
    {
        var sample = new MasterDataSample();
        sample.ComboTable[2] = new ComboTable { ComboMin = 6, ComboMax = 5, Bonus = 3 };

        Assert.Contains("ComboMax", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsANegativeSkinPrice()
    {
        var sample = new MasterDataSample();
        sample.Skins[1] = MasterDataSample.NewSkin("MONO", -1);

        Assert.Contains("Price", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsAGlyphThatIsNotOneCellWide()
    {
        var sample = new MasterDataSample();
        sample.Skins[0] = MasterDataSample.NewSkin("CLASSIC", 0) with { BlockGlyph = "#" };

        Assert.Contains("BlockGlyph", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsAnEmptyPaletteEntry()
    {
        var sample = new MasterDataSample();
        sample.Skins[0] = MasterDataSample.NewSkin("CLASSIC", 0) with { ColorZ = "" };

        Assert.Contains("every palette color must be set", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsAPaletteColorTheConsoleHasNoNameFor()
    {
        var sample = new MasterDataSample();
        sample.Skins[0] = MasterDataSample.NewSkin("CLASSIC", 0) with { ColorS = "Turquoise" };

        Assert.Contains("must name a ConsoleColor", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsACatalogWhereEverySkinCostsSomething()
    {
        var sample = new MasterDataSample();
        sample.Skins[0] = MasterDataSample.NewSkin("CLASSIC", 100);

        Assert.Contains("costs nothing", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsAMissionThatCanNeverBeStarted()
    {
        var sample = new MasterDataSample();
        sample.Missions[0] = sample.Missions[0] with { Goal = 0 };

        Assert.Contains("Goal", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsASecondEconomyRow()
    {
        var sample = new MasterDataSample();
        sample.Economy.Add(sample.Economy[0] with { Id = 2 });

        Assert.Contains("expected exactly one row", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsAnEconomyWithoutAStartingRating()
    {
        var sample = new MasterDataSample();
        sample.Economy[0] = sample.Economy[0] with { RatingInitial = new Rating(0) };

        Assert.Contains("RatingInitial", Failures(sample));
    }

    [Fact]
    public void Validate_RejectsAGameConfigWithoutATickRate()
    {
        var sample = new MasterDataSample();
        sample.GameConfig = sample.GameConfig with { TickRate = 0 };

        Assert.Contains("TickRate", Failures(sample));
    }

    private static string Failures(MasterDataSample sample)
    {
        var result = sample.ToDatabase().Validate();

        Assert.True(result.IsValidationFailed);
        return result.FormatFailedResults();
    }
}
