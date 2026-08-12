using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using Spectre.Console;

namespace SharpCampus.Cli;

// Everything a duel room pushes arrives on the hub's receive loop, while the command thread applies
// the snapshot it asked for; the lock is what keeps those two off each other's replica.
internal sealed class DuelReceiver : IDuelHubReceiver
{
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource<MatchStartInfo> _starting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<MatchResult> _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DuelReplica Replica { get; } = new();

    public Task<MatchStartInfo> Starting => _starting.Task;

    public Task<MatchResult> Finished => _finished.Task;

    public void ApplySnapshot(DuelSnapshot snapshot)
    {
        lock (_gate)
        {
            Replica.ApplySnapshot(snapshot);
        }
    }

    public void OnMatchStarting(MatchStartInfo info)
    {
        lock (_gate)
        {
            Replica.ApplyMatchStart(info);
        }

        AnsiConsole.MarkupLineInterpolated(
            $"[green]Match starting:[/] {info.Players[0].DisplayName} vs {info.Players[1].DisplayName}");
        _starting.TrySetResult(info);
    }

    public void OnTickDelta(TickDelta delta)
    {
        lock (_gate)
        {
            if (!Replica.ApplyTickDelta(delta))
            {
                return;
            }
        }

        foreach (var duelEvent in delta.Events)
        {
            Report(duelEvent);
        }
    }

    public void OnMatchFinished(MatchResult result)
    {
        lock (_gate)
        {
            Replica.ApplyMatchFinished(result);
        }

        _finished.TrySetResult(result);
    }

    private static void Report(DuelEvent duelEvent)
    {
        switch (duelEvent.Kind)
        {
            case TickEventKind.LinesCleared:
                AnsiConsole.MarkupLineInterpolated(
                    $"[grey]P{duelEvent.PlayerIndex} cleared {duelEvent.Value} line(s), combo {duelEvent.Extra}[/]");
                break;
            case TickEventKind.GarbageSent:
                AnsiConsole.MarkupLineInterpolated($"[yellow]P{duelEvent.PlayerIndex} sent {duelEvent.Value} garbage row(s)[/]");
                break;
            case TickEventKind.GarbageApplied:
                AnsiConsole.MarkupLineInterpolated($"[red]P{duelEvent.PlayerIndex} took {duelEvent.Value} garbage row(s)[/]");
                break;
        }
    }
}
