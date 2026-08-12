using MessagePack;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.Shared.Tests;

public class PlayerIndexTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Constructor_TakesEitherSeat(int seat) => Assert.Equal(seat, new PlayerIndex(seat).AsPrimitive());

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Constructor_RejectsAnythingThatIsNotASeat(int seat)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _ = new PlayerIndex(seat));

    [Fact]
    public void RoundTrips() => Assert.Equal(new PlayerIndex(1), Roundtrip(new PlayerIndex(1)));

    // The formatter deserialises through the constructor, so a peer that puts any other number on the
    // wire is rejected at the boundary instead of indexing a seat that does not exist.
    [Fact]
    public void Deserialize_RejectsASeatThatIsNotOnTheBoard()
    {
        var failure = Assert.Throws<MessagePackSerializationException>(() => DeserializeSeat(7));

        Assert.IsType<ArgumentOutOfRangeException>(failure.InnerException);
    }

    private static PlayerIndex Roundtrip(PlayerIndex value)
        => MessagePackSerializer.Deserialize<PlayerIndex>(MessagePackSerializer.Serialize(value));

    private static PlayerIndex DeserializeSeat(int seat)
        => MessagePackSerializer.Deserialize<PlayerIndex>(MessagePackSerializer.Serialize(seat));
}
