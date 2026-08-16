using Cysharp.Text;
using SharpCampus.Cli.Resources;
using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;

namespace SharpCampus.Cli.Duel;

// Owns the console for the length of a match: one task redraws the frame, one flushes queued inputs
// to the room, and one reads the keyboard.
internal sealed class DuelSession(IDuelHub hub, DuelReceiver receiver, DuelRenderer renderer, bool autoPlay)
{
    private const string CursorHome = "\u001b[H";
    private const string ClearScreen = "\u001b[2J\u001b[H";
    private const string ClearBelow = "\u001b[J";

    private static readonly TimeSpan _framePeriod = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan _inputPeriod = TimeSpan.FromMilliseconds(50);

    // Random play for the human seat in unattended verification runs. The other seat is filled by a
    // bot from the bot server, which plays properly.
    private static readonly GameInput[] _autoMoves =
    [
        GameInput.MoveLeft,
        GameInput.MoveRight,
        GameInput.RotateCw,
        GameInput.RotateCcw,
        GameInput.Hold,
    ];

    private string _notice = string.Empty;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var input = autoPlay ? null : new DuelInput();
        var pump = Task.CompletedTask;

        Enter();
        try
        {
            if (input is not null)
            {
                pump = input.PumpAsync(stopping.Token);
            }

            await Task.WhenAll(RenderAsync(stopping.Token), SendAsync(input, stopping.Token));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await stopping.CancelAsync();
            await SettleAsync(pump);
            Leave();
        }
    }

    private static async Task SettleAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RenderAsync(CancellationToken cancellationToken)
    {
        while (!receiver.Finished.IsCompleted)
        {
            Draw();
            await Task.Delay(_framePeriod, cancellationToken);
        }

        // The last frame stays on screen: the result lines are printed underneath it.
        Draw();
    }

    private async Task SendAsync(DuelInput? input, CancellationToken cancellationToken)
    {
        while (!receiver.Finished.IsCompleted)
        {
            await Task.Delay(_inputPeriod, cancellationToken);

            if (receiver.Finished.IsCompleted)
            {
                return;
            }

            if (input is null)
            {
                await hub.SendInputsAsync(AutoInputs());
                continue;
            }

            if (input.TakeForfeit())
            {
                await hub.ForfeitAsync();
                continue;
            }

            if (input.Drain() is { Length: > 0 } inputs)
            {
                await hub.SendInputsAsync(inputs);
            }
        }
    }

    private void Draw()
    {
        var frame = receiver.Read(renderer, static (r, replica) => r.BuildFrame(replica));
        Console.Out.Write(ZString.Concat(CursorHome, frame, _notice));
    }

    private void Enter()
    {
        // Production note: an ANSI-capable terminal is assumed. A shipping client would detect the
        // terminal's capabilities and fall back to a plain scrolling view when they are missing.
        Console.Out.Write(ClearScreen);

        // Cursor and window queries need a console behind the handle, which a piped verification run
        // does not have.
        if (Console.IsOutputRedirected)
        {
            return;
        }

        Console.CursorVisible = false;

        if (Console.WindowWidth < DuelRenderer.MinimumWidth || Console.WindowHeight < DuelRenderer.MinimumHeight)
        {
            _notice = ZString.Concat(
                Localization.Format(Strings.WindowTooSmall, DuelRenderer.MinimumWidth, DuelRenderer.MinimumHeight),
                DuelRenderer.LineEnd);
        }
    }

    private static void Leave()
    {
        Console.Out.Write(ClearBelow);

        if (!Console.IsOutputRedirected)
        {
            Console.CursorVisible = true;
        }

        Console.Out.Write("\r\n");
    }

    private static GameInput[] AutoInputs()
    {
        var inputs = new GameInput[Random.Shared.Next(1, 4)];
        for (var i = 0; i < inputs.Length; i++)
        {
            inputs[i] = _autoMoves[Random.Shared.Next(_autoMoves.Length)];
        }

        // Without the occasional drop the pieces only ever land on gravity, and a match takes minutes.
        if (Random.Shared.Next(4) == 0)
        {
            inputs[^1] = GameInput.HardDrop;
        }

        return inputs;
    }
}
