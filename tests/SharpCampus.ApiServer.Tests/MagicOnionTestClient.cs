using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion;
using MagicOnion.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpCampus.Shared.Serialization;

namespace SharpCampus.ApiServer.Tests;

/// Owns every channel a test opens, so each test disposes them before the next one starts.
internal sealed class MagicOnionTestClient : IDisposable
{
    private readonly List<GrpcChannel> _channels = [];

    /// Calls the in-memory test server over gRPC, optionally as the owner of <paramref name="token"/>.
    public T Create<T>(WebApplicationFactory<Program> factory, string? token = null)
        where T : IService<T>
    {
        var channel = GrpcChannel.ForAddress(
            factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() });
        _channels.Add(channel);

        var client = MagicOnionClient.Create<T>(channel, ContractSerialization.Provider);

        return token is null
            ? client
            : client.WithHeaders(new Metadata { { "authorization", $"Bearer {token}" } });
    }

    public void Dispose()
    {
        foreach (var channel in _channels)
        {
            channel.Dispose();
        }
    }
}
