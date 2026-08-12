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

    private static IEntityType FindProfileType()
    {
        using var db = new SharpCampusDbContext(new DbContextOptionsBuilder<SharpCampusDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=postgres")
            .Options);

        return db.Model.FindEntityType(typeof(Profile))!;
    }

    private static IProperty FindProperty(IEntityType entityType, string name) => entityType.FindProperty(name)!;

    private static object? FindConverter(IEntityType entityType, string name) =>
        FindProperty(entityType, name).GetValueConverter();
}
