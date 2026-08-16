using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.Extensions.Options;
using SharpCampus.Shared.Internal.Bots;
using SharpCampus.Shared.Serialization;

namespace SharpCampus.ApiServer.Matchmaking;

// The same server to server shape as the room control client: one contract, one generated client, one
// long-lived channel to another one of our processes.
public sealed class BotSummonerClient(IOptions<BotFallbackOptions> options) : IBotSummoner, IDisposable
{
    private readonly GrpcChannel _channel = GrpcChannel.ForAddress(options.Value.ControlEndpoint);

    public async Task<SummonBotResult?> SummonAsync()
    {
        var client = MagicOnionClient.Create<IBotControlService>(_channel, ContractSerialization.Provider);

        // Running without a bot server is the everyday case while developing, and every other way this
        // call can fail says the same thing: no bot. Nothing about the pairing pass that asked depends
        // on the answer, and it must not lose its pass over it.
        try
        {
            return await client.SummonAsync();
        }
        catch (RpcException)
        {
            return null;
        }
    }

    public void Dispose() => _channel.Dispose();
}
