using System.Collections.Concurrent;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Shared.Internal.Rooms;
using SharpCampus.Shared.Serialization;

namespace SharpCampus.ApiServer.Matchmaking;

// Server to server calls are ordinary MagicOnion: the same contract type, the same generated client,
// pointed at another one of our processes. Channels are long lived, so one is kept per room server.
public sealed class RoomControlClient : IRoomControlClient, IDisposable
{
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new(StringComparer.Ordinal);

    public async Task<CreateRoomResult?> CreateRoomAsync(string controlEndpoint, CreateRoomRequest request)
    {
        var client = MagicOnionClient.Create<IRoomControlService>(
            _channels.GetOrAdd(controlEndpoint, GrpcChannel.ForAddress),
            ContractSerialization.Provider);

        // A room server that is down is the everyday case while developing, and the caller has to put
        // the pair back in the queue rather than lose them.
        try
        {
            return await client.CreateRoomAsync(request);
        }
        catch (RpcException e) when (e.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded)
        {
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }
    }
}
