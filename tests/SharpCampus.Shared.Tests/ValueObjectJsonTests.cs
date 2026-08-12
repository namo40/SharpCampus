using System.Text.Json;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.Shared.Tests;

public sealed class ValueObjectJsonTests
{
    [Fact]
    public void ValueObjects_SerializeAsTheirUnderlyingPrimitive()
    {
        Assert.Equal("\"CLASSIC\"", JsonSerializer.Serialize(new SkinId("CLASSIC")));
        Assert.Equal("\"PLAY_3\"", JsonSerializer.Serialize(new MissionId("PLAY_3")));
        Assert.Equal("200", JsonSerializer.Serialize(new Coins(200)));
        Assert.Equal("1000", JsonSerializer.Serialize(new Rating(1000)));
    }

    [Fact]
    public void ValueObjects_RoundTripThroughJson()
    {
        var skinId = new SkinId("NEON");

        Assert.Equal(skinId, JsonSerializer.Deserialize<SkinId>(JsonSerializer.Serialize(skinId)));
    }

    [Fact]
    public void ValueObjects_ReadAsPrimitivesInsideARecord()
    {
        // The importer relies on this: no converter is registered anywhere, the generated ones are
        // attributed on the value objects themselves.
        const string json = """{"SkinId":"MONO","Price":200}""";

        var skin = JsonSerializer.Deserialize<Skin>(json);

        Assert.NotNull(skin);
        Assert.Equal(new SkinId("MONO"), skin.SkinId);
        Assert.Equal(200, skin.Price.AsPrimitive());
    }
}
