using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public class SmokeTest
{
    [Fact]
    public void TestProjectRuns()
    {
        Assert.Equal(4, 2 + 2);
    }
}
