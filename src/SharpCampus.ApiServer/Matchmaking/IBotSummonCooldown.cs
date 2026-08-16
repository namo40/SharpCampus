namespace SharpCampus.ApiServer.Matchmaking;

public interface IBotSummonCooldown
{
    // How long ago the last summon was attempted, whatever came of it: a deployed bot spends a few
    // seconds signing in before it reaches the queue, and an attempt that brought none is paced the same
    // rather than repeated every pass.
    Task<bool> IsActiveAsync();

    Task StartAsync();
}
