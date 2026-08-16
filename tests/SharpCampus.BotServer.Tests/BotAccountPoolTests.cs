using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharpCampus.BotServer.Bots;
using SharpCampus.BotServer.Configuration;
using Xunit;

namespace SharpCampus.BotServer.Tests;

public class BotAccountPoolTests
{
    private const string Token = "bot-access-token";

    private readonly IBotAuthClient _auth = Substitute.For<IBotAuthClient>();
    private readonly IBotProfileClient _profiles = Substitute.For<IBotProfileClient>();

    public BotAccountPoolTests()
    {
        _auth.SignInAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(new BotAuthResult(Token, null));
        _profiles.RenameAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
    }

    [Fact]
    public void LeasedAccount_WearsItsSlotNumberAndIsNotSignedInYet()
    {
        var account = CreatePool(2).Lease();

        Assert.NotNull(account);
        Assert.Equal(0, account.Slot);
        Assert.Equal("bot1", account.Nickname);
        Assert.Equal("bot1@sharpcampus.dev", account.Email);
        Assert.Equal(string.Empty, account.AccessToken);
    }

    [Fact]
    public void PoolUnderItsOwnPatterns_NamesItsAccountsApartFromEveryOtherPool()
    {
        var options = new BotServerOptions
        {
            AccountPoolSize = 1,
            EmailPattern = "loadtest{0}@sharpcampus.dev",
            NicknamePattern = "load{0}",
        };

        var account = new BotAccountPool(_auth, _profiles, Options.Create(options), NullLogger<BotAccountPool>.Instance)
            .Lease();

        Assert.NotNull(account);
        Assert.Equal("load1", account.Nickname);
        Assert.Equal("loadtest1@sharpcampus.dev", account.Email);
    }

    [Fact]
    public async Task SignedInAccount_CarriesATokenAndWearsItsNameOnItsProfile()
    {
        var pool = CreatePool(1);
        var account = pool.Lease()!;

        Assert.True(await pool.EnsureSignedInAsync(account));

        Assert.Equal(Token, account.AccessToken);
        await _profiles.Received(1).RenameAsync(Token, "bot1");
    }

    [Fact]
    public void EveryLease_TakesADifferentAccount()
    {
        var pool = CreatePool(3);

        var first = pool.Lease();
        var second = pool.Lease();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Slot, second.Slot);
    }

    [Fact]
    public void PoolWithEveryAccountOut_HasNothingLeftToLease()
    {
        var pool = CreatePool(1);

        Assert.NotNull(pool.Lease());
        Assert.Null(pool.Lease());
    }

    [Fact]
    public void ReturnedAccount_IsAvailableAgain()
    {
        var pool = CreatePool(1);
        var account = pool.Lease()!;

        pool.Return(account);

        Assert.NotNull(pool.Lease());
    }

    [Fact]
    public void AccountsOut_AreTheBotsInAMatch()
    {
        var pool = CreatePool(2);

        Assert.Equal(0, pool.LeasedCount);

        var first = pool.Lease()!;
        pool.Lease();

        Assert.Equal(2, pool.LeasedCount);

        pool.Return(first);

        Assert.Equal(1, pool.LeasedCount);
    }

    [Fact]
    public async Task SecondRunOfASlot_ReusesTheTokenAndTheNameItAlreadyHas()
    {
        var pool = CreatePool(1);
        var first = pool.Lease()!;
        await pool.EnsureSignedInAsync(first);
        pool.Return(first);

        Assert.True(await pool.EnsureSignedInAsync(pool.Lease()!));

        await _auth.Received(1).SignInAsync(Arg.Any<string>(), Arg.Any<string>());
        await _profiles.Received(1).RenameAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task AccountTheAuthServerHasNeverSeen_IsSignedUpAndThenSignedIn()
    {
        _auth.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new BotAuthResult(null, "Invalid login credentials"), new BotAuthResult(Token, null));

        var pool = CreatePool(1);
        var account = pool.Lease()!;

        Assert.True(await pool.EnsureSignedInAsync(account));

        Assert.Equal(Token, account.AccessToken);
        await _auth.Received(1).SignUpAsync("bot1@sharpcampus.dev", Arg.Any<string>());
    }

    [Fact]
    public async Task AuthServerThatWillNotSignTheBotIn_LeavesTheAccountWithoutATokenToRetryWith()
    {
        _auth.SignInAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(new BotAuthResult(null, "supabase is down"));

        var pool = CreatePool(1);
        var account = pool.Lease()!;

        Assert.False(await pool.EnsureSignedInAsync(account));

        Assert.Equal(string.Empty, account.AccessToken);

        _auth.SignInAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(new BotAuthResult(Token, null));

        Assert.True(await pool.EnsureSignedInAsync(account));
    }

    [Fact]
    public async Task RefreshedAccount_CarriesAFreshToken()
    {
        _auth.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new BotAuthResult("expired", null), new BotAuthResult(Token, null));

        var pool = CreatePool(1);
        var account = pool.Lease()!;
        await pool.EnsureSignedInAsync(account);

        Assert.True(await pool.RefreshAsync(account));

        Assert.Equal(Token, account.AccessToken);
    }

    private BotAccountPool CreatePool(int size) => new(
        _auth,
        _profiles,
        Options.Create(new BotServerOptions { AccountPoolSize = size }),
        NullLogger<BotAccountPool>.Instance);
}
