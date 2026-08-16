namespace SharpCampus.BotServer.Bots;

public interface IBotRunner
{
    // Plays one match out, from joining the queue to the result. Behind an interface so the control
    // service can be exercised without a queue to join.
    Task PlayAsync(BotAccount account, CancellationToken cancellationToken);
}
