using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Missions;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.Shared.Tests;

public sealed class MasterDataSchemaTests
{
    [Fact]
    public void GeneratedIndexes_LookRowsUpByTheirPrimaryKey()
    {
        var database = new MasterDataSample().ToDatabase();

        Assert.Equal(18, database.GravityCurveTable.FindByLevel(2).TicksPerCell);
        Assert.Equal(4, database.AttackTableTable.FindByLinesCleared(4).Garbage);
        Assert.Equal(2, database.ComboTableTable.FindByComboMin(4).Bonus);
        Assert.Equal(20, database.GameConfigTable.FindById(GameConfig.SingleRowId).TickRate);
    }

    [Fact]
    public void GeneratedIndexes_LookRowsUpByAValueObjectKey()
    {
        var database = new MasterDataSample().ToDatabase();

        Assert.Equal(200, database.SkinTable.FindBySkinId(new SkinId("MONO")).Price.AsPrimitive());
        Assert.True(database.SkinTable.TryFindBySkinId(new SkinId("RETRO"), out var retro));
        Assert.Equal("skin.retro.name", retro.NameKey);
        Assert.False(database.SkinTable.TryFindBySkinId(new SkinId("NEON"), out _));
    }

    [Fact]
    public void ValueObjectKeys_SortTheTableOrdinally()
    {
        var sample = new MasterDataSample();
        sample.Skins = [MasterDataSample.NewSkin("RETRO", 400), MasterDataSample.NewSkin("CLASSIC", 0), MasterDataSample.NewSkin("MONO", 200)];

        var order = sample.ToDatabase().SkinTable.All.Select(skin => skin.SkinId.AsPrimitive()).ToArray();

        Assert.Equal(["CLASSIC", "MONO", "RETRO"], order);
    }

    [Fact]
    public void FreeSkin_IsResolvedOnceTheDatabaseIsBuilt()
    {
        var database = new MasterDataSample().ToDatabase();

        Assert.Equal(new SkinId("CLASSIC"), database.SkinTable.FreeSkin.SkinId);
    }

    [Fact]
    public void Binary_PreservesValueObjectsAndEnums()
    {
        var database = new MasterDataSample().ToDatabase();

        var mission = database.MissionTable.FindByMissionId(new MissionId("WIN_1"));
        Assert.Equal(MissionMetric.Wins, mission.Metric);
        Assert.Equal(50, mission.RewardCoins.AsPrimitive());
        Assert.Equal(1000, database.EconomyTable.FindById(GameConfig.SingleRowId).RatingInitial.AsPrimitive());
    }
}
