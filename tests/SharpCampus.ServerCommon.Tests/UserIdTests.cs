using MessagePack;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Identity;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class UserIdTests
{
    [Fact]
    public void UserId_SurvivesAMessagePackRoundTrip()
    {
        var userId = UserId.New();

        Assert.Equal(userId, Deserialize<UserId>(Serialize(userId)));
    }

    [Fact]
    public void UserId_TravelsInsideADto()
    {
        var identity = new IdentityResponse(UserId.New(), "player@example.com");

        Assert.Equal(identity, Deserialize<IdentityResponse>(Serialize(identity)));
    }

    [Fact]
    public void UserId_SerializesAsItsUnderlyingGuid()
    {
        var value = Guid.NewGuid();

        Assert.Equal(Serialize(value), Serialize(new UserId(value)));
    }

    [Fact]
    public void UserId_ParsesTheSubjectClaimFormat()
    {
        var value = Guid.NewGuid();

        Assert.Equal(new UserId(value), UserId.Parse(value.ToString()));
    }

    private static byte[] Serialize<T>(T value) =>
        MessagePackSerializer.Serialize(value, cancellationToken: TestContext.Current.CancellationToken);

    private static T Deserialize<T>(byte[] bytes) =>
        MessagePackSerializer.Deserialize<T>(bytes, cancellationToken: TestContext.Current.CancellationToken);
}
