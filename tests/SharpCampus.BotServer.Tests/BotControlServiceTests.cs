using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharpCampus.BotServer.Bots;
using SharpCampus.BotServer.Configuration;
using SharpCampus.Shared.Internal.Bots;
using Xunit;

namespace SharpCampus.BotServer.Tests;

public class BotControlServiceTests
{
    private readonly IBotAuthClient _auth = Substitute.For<IBotAuthClient>();
    private readonly IBotProfileClient _profiles = Substitute.For<IBotProfileClient>();
    private readonly IBotRunner _runner = Substitute.For<IBotRunner>();

    // A bot that is playing until the test says otherwise, which is what holds its account out.
    private readonly TaskCompletionSource _playing = new();

    public BotControlServiceTests()
    {
        _auth.SignInAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(new BotAuthResult("token", null));
        _runner.PlayAsync(Arg.Any<BotAccount>(), Arg.Any<CancellationToken>()).Returns(_playing.Task);
    }

    [Fact]
    public async Task Summon_LeasesAnAccountAndSetsItPlaying()
    {
        var result = await CreateService(1).SummonAsync();

        Assert.Equal(SummonBotOutcome.Deployed, result.Outcome);
        await _runner.Received(1).PlayAsync(
            Arg.Is<BotAccount>(account => account.Nickname == "bot1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SummonWithEveryAccountAlreadyPlaying_IsExhausted()
    {
        var service = CreateService(1);
        await service.SummonAsync();

        var result = await service.SummonAsync();

        Assert.Equal(SummonBotOutcome.Exhausted, result.Outcome);
        await _runner.Received(1).PlayAsync(Arg.Any<BotAccount>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AccountOfABotThatFinished_GoesBackIntoThePool()
    {
        var service = CreateService(1);
        await service.SummonAsync();

        _playing.SetResult();

        Assert.Equal(SummonBotOutcome.Deployed, await EventuallySummonedAsync(service));
    }

    [Fact]
    public async Task AccountOfABotThatFailed_GoesBackIntoThePool()
    {
        _runner.PlayAsync(Arg.Any<BotAccount>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("the room went away"));

        var service = CreateService(1);
        await service.SummonAsync();

        // A slot the failure kept would shrink the pool for the life of the process.
        var result = await service.SummonAsync();

        Assert.Equal(SummonBotOutcome.Deployed, result.Outcome);
    }

    [Fact]
    public async Task SummonWithAnAuthServerThatWillNotAnswer_DeploysABotThatNeverArrives()
    {
        _auth.SignInAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(new BotAuthResult(null, "supabase is down"));

        var service = CreateService(1);
        var result = await service.SummonAsync();

        // Signing in happens after the answer, so a summon cannot tell that one failed. Exhausted stays
        // the answer for a pool with nothing free, and nothing else.
        Assert.Equal(SummonBotOutcome.Deployed, result.Outcome);
        await _runner.DidNotReceive().PlayAsync(Arg.Any<BotAccount>(), Arg.Any<CancellationToken>());

        // The slot the bot that never signed in took is free again for a later summon.
        _auth.SignInAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(new BotAuthResult("token", null));

        Assert.Equal(SummonBotOutcome.Deployed, await EventuallySummonedAsync(service));
        await _runner.Received(1).PlayAsync(Arg.Any<BotAccount>(), Arg.Any<CancellationToken>());
    }

    // The bot task is fire and forget, so its account comes back a moment after the runner does.
    private static async Task<SummonBotOutcome> EventuallySummonedAsync(BotControlService service)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if ((await service.SummonAsync()).Outcome == SummonBotOutcome.Deployed)
            {
                return SummonBotOutcome.Deployed;
            }

            await Task.Delay(20);
        }

        return SummonBotOutcome.Exhausted;
    }

    private BotControlService CreateService(int poolSize) => new(
        new BotAccountPool(
            _auth,
            _profiles,
            Options.Create(new BotServerOptions { AccountPoolSize = poolSize }),
            NullLogger<BotAccountPool>.Instance),
        _runner,
        NullLogger<BotControlService>.Instance);
}
