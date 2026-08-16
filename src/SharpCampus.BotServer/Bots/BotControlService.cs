using MagicOnion;
using MagicOnion.Server;
using SharpCampus.Shared.Internal.Bots;

namespace SharpCampus.BotServer.Bots;

// The whole inbound surface of this server. It answers the moment an account is taken: the caller is a
// matchmaking pass that cannot sit out a match, so signing in and queueing happen on the bot's own task.
public sealed class BotControlService(BotAccountPool pool, IBotRunner runner, ILogger<BotControlService> logger)
    : ServiceBase<IBotControlService>, IBotControlService
{
    public UnaryResult<SummonBotResult> SummonAsync()
    {
        if (pool.Lease() is not { } account)
        {
            logger.BotPoolExhausted(pool.Size);
            return UnaryResult.FromResult(SummonBotResult.Exhausted);
        }

        logger.BotSummoned(account.Nickname);
        _ = RunAsync(account);

        return UnaryResult.FromResult(SummonBotResult.Deployed);
    }

    private async Task RunAsync(BotAccount account)
    {
        try
        {
            // A bot that cannot sign in is one that never arrives: the summon has already answered, and
            // the caller's own retry pacing is what brings the next one. The pool logged the reason.
            if (await pool.EnsureSignedInAsync(account))
            {
                await runner.PlayAsync(account, CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            // Nothing may escape a bot task: a slot that is never returned shrinks the pool for the
            // life of the process.
            logger.BotFailed(account.Nickname, e.Message);
        }
        finally
        {
            pool.Return(account);
            logger.BotReleased(account.Nickname);
        }
    }
}
