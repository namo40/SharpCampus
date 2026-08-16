using DFrame;
using SharpCampus.BotServer.Bots;

namespace SharpCampus.LoadTest;

// One execution is one whole match: queue, ticket, room, inputs, result. The player behind it is the
// bot server's, unchanged, so what this puts the servers through is what a console client puts them
// through. Run it at a concurrency of two or more, or the virtual players have nobody to be paired with.
public sealed class DuelMatchWorkload(BotAccountPool pool, IBotRunner runner) : Workload
{
    public override async Task ExecuteAsync(WorkloadContext context)
    {
        var account = pool.Lease()
            ?? throw new InvalidOperationException($"Concurrency is above the {pool.Size} accounts in the pool.");

        try
        {
            if (!await pool.EnsureSignedInAsync(account))
            {
                throw new InvalidOperationException($"Could not sign {account.Email} in. Run: supabase start");
            }

            await runner.PlayAsync(account, context.CancellationToken);
        }
        finally
        {
            pool.Return(account);
        }
    }
}
