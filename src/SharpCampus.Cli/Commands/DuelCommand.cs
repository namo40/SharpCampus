using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.GameCore;
using SharpCampus.Shared.Duel;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

// Temporary: proves the room, the hub contract and the replica end to end while there is no
// playable client. Real play, rendering and the match flow replace this.
[RegisterCommands]
internal sealed class DuelCommand
{
    private const string RoomServerAddress = "http://localhost:5002";

    private static readonly GameInput[] _moves =
    [
        GameInput.MoveLeft,
        GameInput.MoveRight,
        GameInput.RotateCw,
        GameInput.RotateCcw,
        GameInput.Hold,
    ];

    /// <summary>Joins a duel room and plays it out with random inputs.</summary>
    /// <param name="room">Room key. Both clients have to use the same one.</param>
    /// <param name="name">Name the opponent sees.</param>
    /// <param name="cancellationToken">Cancellation of the running match.</param>
    [Command("duel")]
    public async Task ExecuteAsync(
        string room = "test",
        string name = "player",
        CancellationToken cancellationToken = default)
    {
        using var channel = GrpcChannel.ForAddress(RoomServerAddress);
        var receiver = new DuelReceiver();

        try
        {
            var hub = await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
                channel, receiver, cancellationToken: cancellationToken);
            try
            {
                await PlayAsync(hub, receiver, room, name, cancellationToken);
            }
            finally
            {
                await hub.DisposeAsync();
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]RoomServer: unreachable at {RoomServerAddress}[/]");
        }
    }

    private static async Task PlayAsync(
        IDuelHub hub,
        DuelReceiver receiver,
        string room,
        string name,
        CancellationToken cancellationToken)
    {
        var join = await hub.JoinAsync(new JoinRoomRequest(room, name));
        if (join is not { Accepted: true, PlayerIndex: { } seat })
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Room {room} would not seat you.[/]");
            return;
        }

        AnsiConsole.MarkupLineInterpolated(
            $"[green]Seated as player {seat} in {room}.[/] {(join.WaitingForOpponent ? "Waiting for an opponent..." : string.Empty)}");

        try
        {
            await receiver.Starting.WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
        }
        catch (TimeoutException)
        {
            AnsiConsole.MarkupLine("[yellow]No opponent joined.[/]");
            return;
        }

        receiver.ApplySnapshot(await hub.RequestSnapshotAsync());

        while (!receiver.Finished.IsCompleted && !cancellationToken.IsCancellationRequested)
        {
            await hub.SendInputsAsync(NextInputs());
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        var result = await receiver.Finished.WaitAsync(cancellationToken);
        var verdict = result.WinnerPlayerIndex switch
        {
            null => "Draw",
            { } winner when winner == seat => "You win",
            _ => "You lose",
        };

        AnsiConsole.MarkupLineInterpolated($"[green]{verdict}[/] [grey]({result.Outcome} by {result.Reason})[/]");
        AnsiConsole.MarkupLineInterpolated(
            $"[grey]Replica tick {receiver.Replica.Tick.AsPrimitive()}, missed ticks: {receiver.Replica.HasMissedTicks}[/]");
    }

    private static GameInput[] NextInputs()
    {
        var inputs = new GameInput[Random.Shared.Next(1, 4)];
        for (var i = 0; i < inputs.Length; i++)
        {
            inputs[i] = _moves[Random.Shared.Next(_moves.Length)];
        }

        // Without the occasional drop the pieces only ever land on gravity, and a match takes minutes.
        if (Random.Shared.Next(4) == 0)
        {
            inputs[^1] = GameInput.HardDrop;
        }

        return inputs;
    }
}
