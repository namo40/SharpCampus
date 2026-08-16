using DFrame;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.Extensions.Options;
using SharpCampus.BotServer.Bots;
using SharpCampus.BotServer.Configuration;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Services;

namespace SharpCampus.LoadTest;

// One execution is one ranking read. The account and the channel are set up once per virtual player, so
// what each execution times is the call and nothing around it.
public sealed class LeaderboardQueryWorkload(BotAccountPool pool, IOptions<BotServerOptions> options) : Workload
{
    private BotAccount? _account;
    private GrpcChannel? _channel;
    private ILeaderboardService? _leaderboards;

    public override async Task SetupAsync(WorkloadContext context)
    {
        _account = pool.Lease()
            ?? throw new InvalidOperationException($"Concurrency is above the {pool.Size} accounts in the pool.");

        if (!await pool.EnsureSignedInAsync(_account))
        {
            throw new InvalidOperationException($"Could not sign {_account.Email} in. Run: supabase start");
        }

        _channel = GrpcChannel.ForAddress(options.Value.ApiServerAddress);
        _leaderboards = MagicOnionClient.Create<ILeaderboardService>(_channel)
            .WithHeaders(new Metadata { { "authorization", $"Bearer {_account.AccessToken}" } });
    }

    public override async Task ExecuteAsync(WorkloadContext context)
        => await _leaderboards!.WithCancellationToken(context.CancellationToken)
            .GetLeaderboardAsync(LeaderboardKind.Rating);

    public override Task TeardownAsync(WorkloadContext context)
    {
        _channel?.Dispose();

        if (_account is not null)
        {
            pool.Return(_account);
        }

        return Task.CompletedTask;
    }
}
