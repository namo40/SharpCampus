using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SharpCampus.Server.Common.Data;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

// Builds the model only, so nothing here needs a reachable database.
public sealed class SharpCampusDbContextTests
{
    [Fact]
    public void Profiles_MapToTheColumnsOfTheSchemaScript()
    {
        var profile = FindProfileType();
        string[] expected = ["coins", "created_at", "nickname", "rating", "updated_at", "user_id"];

        Assert.Equal("profiles", profile.GetTableName());
        Assert.Equal(expected, profile.GetProperties().Select(p => p.GetColumnName()).Order());
    }

    [Fact]
    public void ValueObjects_AreReadAndWrittenThroughTheGeneratedConverters()
    {
        var profile = FindProfileType();

        Assert.IsType<UserId.UserIdValueConverter>(FindConverter(profile, nameof(Profile.UserId)));
        Assert.IsType<Coins.CoinsValueConverter>(FindConverter(profile, nameof(Profile.Coins)));
        Assert.IsType<Rating.RatingValueConverter>(FindConverter(profile, nameof(Profile.Rating)));
    }

    [Fact]
    public void ColumnsWithADatabaseDefault_AreLeftToTheDatabaseOnInsert()
    {
        var profile = FindProfileType();

        Assert.Equal(ValueGenerated.Never, FindProperty(profile, nameof(Profile.UserId)).ValueGenerated);
        Assert.Equal(ValueGenerated.OnAdd, FindProperty(profile, nameof(Profile.Coins)).ValueGenerated);
        Assert.Equal(ValueGenerated.OnAdd, FindProperty(profile, nameof(Profile.Rating)).ValueGenerated);
    }

    [Fact]
    public void MatchRecords_MapToTheColumnsOfTheSchemaScript()
    {
        var record = FindEntityType(typeof(MatchRecord));
        string[] expected =
        [
            "coins_awarded", "created_at", "duration_ticks", "end_reason", "garbage_sent", "hard_drops",
            "lines_cleared", "match_id", "max_combo", "outcome", "quads", "rating_after", "rating_before",
            "user_id",
        ];

        Assert.Equal("match_records", record.GetTableName());
        Assert.Equal(expected, record.GetProperties().Select(p => p.GetColumnName()).Order());
    }

    [Fact]
    public void AMatchRecord_IsKeyedByTheMatchAndThePlayerTogether()
    {
        var record = FindEntityType(typeof(MatchRecord));

        Assert.Equal(
            [nameof(MatchRecord.MatchId), nameof(MatchRecord.UserId)],
            record.FindPrimaryKey()!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void MatchRecordValueObjects_AreReadAndWrittenThroughTheirConverters()
    {
        var record = FindEntityType(typeof(MatchRecord));

        Assert.IsType<MatchId.MatchIdValueConverter>(FindConverter(record, nameof(MatchRecord.MatchId)));
        Assert.IsType<UserId.UserIdValueConverter>(FindConverter(record, nameof(MatchRecord.UserId)));
        Assert.IsType<Rating.RatingValueConverter>(FindConverter(record, nameof(MatchRecord.RatingBefore)));
        Assert.IsType<Coins.CoinsValueConverter>(FindConverter(record, nameof(MatchRecord.CoinsAwarded)));
    }

    [Fact]
    public void AMatchRecordsTimestamp_IsLeftToTheDatabaseOnInsert()
    {
        var record = FindEntityType(typeof(MatchRecord));

        Assert.Equal(ValueGenerated.OnAdd, FindProperty(record, nameof(MatchRecord.CreatedAt)).ValueGenerated);
        Assert.Equal(ValueGenerated.Never, FindProperty(record, nameof(MatchRecord.RatingAfter)).ValueGenerated);
    }

    private static IEntityType FindProfileType() => FindEntityType(typeof(Profile));

    private static IEntityType FindEntityType(Type type)
    {
        using var db = new SharpCampusDbContext(new DbContextOptionsBuilder<SharpCampusDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=postgres")
            .Options);

        return db.Model.FindEntityType(type)!;
    }

    private static IProperty FindProperty(IEntityType entityType, string name) => entityType.FindProperty(name)!;

    private static object? FindConverter(IEntityType entityType, string name) =>
        FindProperty(entityType, name).GetValueConverter();
}
