using SharpCampus.Shared.Identity;
using SharpCampus.Shared.Profiles;
using Xunit;

namespace SharpCampus.ServerCommon.Tests;

public sealed class NicknameRulesTests
{
    [Theory]
    [InlineData("ab")]
    [InlineData("Player_1")]
    [InlineData("__")]
    [InlineData("0123456789abcdef")]
    public void IsValid_AcceptsAsciiLettersDigitsAndUnderscores(string nickname)
    {
        Assert.True(NicknameRules.IsValid(nickname));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("0123456789abcdefg")]
    [InlineData("two words")]
    [InlineData("dash-ed")]
    [InlineData("한글")]
    [InlineData("nick\U0001F600")]
    [InlineData(null)]
    public void IsValid_RejectsEverythingElse(string? nickname)
    {
        Assert.False(NicknameRules.IsValid(nickname));
    }

    [Fact]
    public void CreateInitial_ProducesAnAcceptedNickname()
    {
        var nickname = NicknameRules.CreateInitial(UserId.New());

        Assert.StartsWith(NicknameRules.InitialPrefix, nickname);
        Assert.Equal(NicknameRules.InitialPrefix.Length + 8, nickname.Length);
        Assert.True(NicknameRules.IsValid(nickname));
    }

    [Fact]
    public void CreateInitial_DependsOnlyOnTheAccount()
    {
        var userId = UserId.New();

        Assert.Equal(NicknameRules.CreateInitial(userId), NicknameRules.CreateInitial(userId));
        Assert.NotEqual(NicknameRules.CreateInitial(userId), NicknameRules.CreateInitial(UserId.New()));
    }
}
