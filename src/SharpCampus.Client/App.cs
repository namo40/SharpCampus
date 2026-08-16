using Grpc.Core;
using SharpCampus.Client.Common;
using SharpCampus.Client.Screens;

namespace SharpCampus.Client;

// What a screen leaves the app to do next.
internal enum AppFlow
{
    Continue,
    Exit,
}

// Owns the session the process is signed in with. Screens never build a gateway of their own: anything
// that replaces or discards the session comes back here to have one built around what is stored now.
internal sealed class App
{
    public async Task RunAsync()
    {
        var session = ClientSession.Load();

        if (session is not null)
        {
            switch (await ResumeAsync(session))
            {
                case Resume.SignedOut:
                    session = null;
                    break;
                case Resume.Quit:
                    return;
            }
        }

        while (true)
        {
            if (session is null)
            {
                if (await new TitleScreen().RunAsync() is not { } signedIn)
                {
                    return;
                }

                session = signedIn;
            }

            using var gateway = new ApiGateway(session);
            if (await new LobbyScreen(gateway).RunAsync() == AppFlow.Exit)
            {
                return;
            }

            // Logging out deletes the file and linking rewrites it, so reading it back is how the next
            // round learns which of the two happened.
            session = ClientSession.Load();
        }
    }

    // A stored session is only worth resuming if the server still takes it, which is one call to ask.
    private static async Task<Resume> ResumeAsync(ClientSession session)
    {
        using var gateway = new ApiGateway(session);

        while (true)
        {
            try
            {
                await gateway.Account.GetMyIdentityAsync();
                return Resume.Ready;
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
            {
                ClientSession.Delete();
                return Resume.SignedOut;
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
            {
                if (!Ui.RetryRequested())
                {
                    return Resume.Quit;
                }
            }
        }
    }

    private enum Resume
    {
        Ready,
        SignedOut,
        Quit,
    }
}
