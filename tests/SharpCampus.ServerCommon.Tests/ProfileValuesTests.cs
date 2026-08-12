using MessagePack;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class ProfileValuesTests
{
    [Fact]
    public void Coins_SurviveAMessagePackRoundTrip()
    {
        Assert.Equal(new Coins(4200), Deserialize<Coins>(Serialize(new Coins(4200))));
    }

    [Fact]
    public void Rating_SurvivesAMessagePackRoundTrip()
    {
        Assert.Equal(new Rating(1180), Deserialize<Rating>(Serialize(new Rating(1180))));
    }

    [Fact]
    public void ProfileValues_SerializeAsTheirUnderlyingInt()
    {
        Assert.Equal(Serialize(1180), Serialize(new Rating(1180)));
        Assert.Equal(Serialize(4200), Serialize(new Coins(4200)));
    }

    [Fact]
    public void ProfileValues_TravelInsideADto()
    {
        var profile = new ProfileResponse(UserId.New(), "boardsweeper", new Coins(4200), new Rating(1180));

        Assert.Equal(profile, Deserialize<ProfileResponse>(Serialize(profile)));
    }

    private static byte[] Serialize<T>(T value) =>
        MessagePackSerializer.Serialize(value, cancellationToken: TestContext.Current.CancellationToken);

    private static T Deserialize<T>(byte[] bytes) =>
        MessagePackSerializer.Deserialize<T>(bytes, cancellationToken: TestContext.Current.CancellationToken);
}
