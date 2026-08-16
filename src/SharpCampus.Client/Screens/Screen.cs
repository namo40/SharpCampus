using Grpc.Core;
using SharpCampus.Client.Common;
using SharpCampus.Client.Resources;

namespace SharpCampus.Client.Screens;

// How a screen ended, and what the lobby has to do about it.
internal enum ScreenResult
{
    // Back to whatever opened the screen.
    Back,

    // The stored session is gone, so the app returns to the title screen.
    SignedOut,

    // The stored session was replaced, so the gateway has to be rebuilt around the new one.
    SessionChanged,
}

// The two failures every screen shares: a token the server will not take, and a server that is not
// there. Both are answered the same way wherever they happen, so they are caught once here.
internal abstract class Screen(ApiGateway gateway)
{
    protected ApiGateway Gateway { get; } = gateway;

    public async Task<ScreenResult> RunAsync()
    {
        try
        {
            return await ShowAsync();
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            ClientSession.Delete();
            Ui.Error(Strings.SessionEndedNotice);
            Ui.Pause();
            return ScreenResult.SignedOut;
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            Ui.Error(Unreachable(e));
            Ui.Pause();
            return ScreenResult.Back;
        }
    }

    protected abstract Task<ScreenResult> ShowAsync();

    // Only a duel talks to anything other than the ApiServer, and only it knows which room server.
    protected virtual string Unreachable(RpcException exception) => Ui.UnreachableMessage();
}
