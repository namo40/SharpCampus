using System.Collections.Concurrent;
using System.Diagnostics;
using SharpCampus.GameCore;

namespace SharpCampus.Cli.Duel;

// A console delivers key presses but never key releases, so soft drop is released on a gap in the
// OS auto-repeat. DAS and ARR are that same auto-repeat: one delivered key is one command.
internal sealed class DuelInput
{
    private static readonly TimeSpan _pollPeriod = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan _softDropRelease = TimeSpan.FromMilliseconds(200);

    private readonly ConcurrentQueue<GameInput> _pending = new();

    private long _softDropAt;
    private bool _softDropHeld;
    private int _forfeit;

    // Blocking ReadKey cannot be cancelled, so the keyboard is polled instead.
    public async Task PumpAsync(CancellationToken cancellationToken)
    {
        // A redirected stdin has no key events to read; the match then runs on gravity alone.
        if (Console.IsInputRedirected)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            while (Console.KeyAvailable)
            {
                Accept(Console.ReadKey(intercept: true));
            }

            ReleaseSoftDrop();
            await Task.Delay(_pollPeriod, cancellationToken);
        }
    }

    public bool TakeForfeit() => Interlocked.Exchange(ref _forfeit, 0) == 1;

    public GameInput[] Drain()
    {
        if (_pending.IsEmpty)
        {
            return [];
        }

        var inputs = new List<GameInput>();
        while (_pending.TryDequeue(out var input))
        {
            inputs.Add(input);
        }

        return [.. inputs];
    }

    private void Accept(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                _pending.Enqueue(GameInput.MoveLeft);
                break;
            case ConsoleKey.RightArrow:
                _pending.Enqueue(GameInput.MoveRight);
                break;
            case ConsoleKey.DownArrow:
                HoldSoftDrop();
                break;
            case ConsoleKey.UpArrow or ConsoleKey.X:
                _pending.Enqueue(GameInput.RotateCw);
                break;
            case ConsoleKey.Z:
                _pending.Enqueue(GameInput.RotateCcw);
                break;
            case ConsoleKey.Spacebar:
                _pending.Enqueue(GameInput.HardDrop);
                break;
            case ConsoleKey.C:
                _pending.Enqueue(GameInput.Hold);
                break;
            case ConsoleKey.Q:
                Interlocked.Exchange(ref _forfeit, 1);
                break;
        }
    }

    private void HoldSoftDrop()
    {
        _softDropAt = Stopwatch.GetTimestamp();

        if (_softDropHeld)
        {
            return;
        }

        _softDropHeld = true;
        _pending.Enqueue(GameInput.SoftDropOn);
    }

    private void ReleaseSoftDrop()
    {
        if (!_softDropHeld || Stopwatch.GetElapsedTime(_softDropAt) < _softDropRelease)
        {
            return;
        }

        _softDropHeld = false;
        _pending.Enqueue(GameInput.SoftDropOff);
    }
}
