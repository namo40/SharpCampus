using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Values;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class ValueConverterTests
{
    [Fact]
    public void UserId_ConvertsToAndFromItsColumnValue()
    {
        var converter = new UserId.UserIdValueConverter();
        var userId = UserId.New();

        Assert.Equal(userId.AsPrimitive(), converter.ConvertToProvider(userId));
        Assert.Equal(userId, converter.ConvertFromProvider(userId.AsPrimitive()));
    }

    [Fact]
    public void Coins_ConvertToAndFromTheirColumnValue()
    {
        var converter = new Coins.CoinsValueConverter();

        Assert.Equal(4200, converter.ConvertToProvider(new Coins(4200)));
        Assert.Equal(new Coins(4200), converter.ConvertFromProvider(4200));
    }

    [Fact]
    public void Rating_ConvertsToAndFromItsColumnValue()
    {
        var converter = new Rating.RatingValueConverter();

        Assert.Equal(1180, converter.ConvertToProvider(new Rating(1180)));
        Assert.Equal(new Rating(1180), converter.ConvertFromProvider(1180));
    }
}
