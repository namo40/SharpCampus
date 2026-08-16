using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion;
using MagicOnion.Client;
using SharpCampus.Client.Common;
using SharpCampus.Shared.Services;

namespace SharpCampus.Client;

// One channel and one set of clients for as long as a session lasts. Signing in, linking and signing
// out all replace the session, and a gateway built around the old bearer token goes with it.
internal sealed class ApiGateway : IDisposable
{
    private readonly GrpcChannel _channel;

    public ApiGateway(ClientSession session)
    {
        Session = session;
        Authorization = new Metadata { { "authorization", $"Bearer {session.AccessToken}" } };

        _channel = GrpcChannel.ForAddress(ClientEndpoints.ApiServer);

        Account = Create<IAccountService>();
        Profile = Create<IProfileService>();
        Shop = Create<IShopService>();
        Missions = Create<IMissionService>();
        Leaderboard = Create<ILeaderboardService>();
        History = Create<IMatchHistoryService>();
        Matchmaking = Create<IMatchmakingService>();
    }

    public ClientSession Session { get; }

    // The duel hub needs these beside its own room header, so they are kept rather than rebuilt.
    public Metadata Authorization { get; }

    public IAccountService Account { get; }

    public IProfileService Profile { get; }

    public IShopService Shop { get; }

    public IMissionService Missions { get; }

    public ILeaderboardService Leaderboard { get; }

    public IMatchHistoryService History { get; }

    public IMatchmakingService Matchmaking { get; }

    public void Dispose() => _channel.Dispose();

    private T Create<T>() where T : IService<T> =>
        MagicOnionClient.Create<T>(_channel).WithHeaders(Authorization);
}
