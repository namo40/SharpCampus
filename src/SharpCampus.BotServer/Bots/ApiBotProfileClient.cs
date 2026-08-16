using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.Extensions.Options;
using SharpCampus.BotServer.Configuration;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Services;

namespace SharpCampus.BotServer.Bots;

// The bot renames its own profile through the ordinary client contract, carrying its own token: the
// ApiServer has no idea it is talking to a bot.
internal sealed class ApiBotProfileClient(IOptions<BotServerOptions> options) : IBotProfileClient, IDisposable
{
    private readonly GrpcChannel _channel = GrpcChannel.ForAddress(options.Value.ApiServerAddress);

    public async Task<bool> RenameAsync(string accessToken, string nickname)
    {
        var profiles = MagicOnionClient.Create<IProfileService>(_channel)
            .WithHeaders(new Metadata { { "authorization", $"Bearer {accessToken}" } });

        try
        {
            return await profiles.UpdateNicknameAsync(nickname) == NicknameUpdateResult.Updated;
        }
        catch (RpcException)
        {
            // Best effort by definition: an ApiServer that cannot be reached here is about to say so
            // again on the queue call, which is the one that matters.
            return false;
        }
    }

    public void Dispose() => _channel.Dispose();
}
