using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.GameCore;
using SharpCampus.Shared.Dtos;
using SharpCampus.Shared.Duel;
using SharpCampus.Shared.Services;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

// Temporary: proves matchmaking, the room, the hub contract and the replica end to end while there is
// no playable client. Real play, rendering and the match flow replace this.
[RegisterCommands]
internal sealed class DuelCommand
{
    private const string ApiServerAddress = "http://localhost:5001";
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(1);

    private static readonly GameInput[] _moves =
    [
        GameInput.MoveLeft,
        GameInput.MoveRight,
        GameInput.RotateCw,
        GameInput.RotateCcw,
        GameInput.Hold,
    ];

    /// <summary>Queues for a duel and plays the match out with random inputs.</summary>
    /// <param name="cancellationToken">Cancellation of the queue wait or the running match.</param>
    [Command("duel")]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (ClientSession.Load() is not { } session)
        {
            AnsiConsole.MarkupLine("[yellow]Not logged in. Run: login <email> <password>[/]");
            return;
        }

        var authorization = new Metadata { { "authorization", $"Bearer {session.AccessToken}" } };

        using var apiChannel = GrpcChannel.ForAddress(ApiServerAddress);
        var matchmaking = MagicOnionClient.Create<IMatchmakingService>(apiChannel).WithHeaders(authorization);

        try
        {
            if (await WaitForTicketAsync(matchmaking, cancellationToken) is { } ticket)
            {
                await PlayAsync(ticket, authorization, cancellationToken);
            }
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unauthenticated)
        {
            AnsiConsole.MarkupLine("[red]Session expired or invalid. Run: login <email> <password>[/]");
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Server unreachable: {e.Status.Detail}[/]");
        }
    }

    private static async Task<MatchTicket?> WaitForTicketAsync(
        IMatchmakingService matchmaking,
        CancellationToken cancellationToken)
    {
        var status = await matchmaking.EnqueueAsync();
        AnsiConsole.MarkupLine("[green]Queued. Waiting for an opponent...[/]");

        // Production note: a live service pushes the match to the client. Polling keeps the contract to
        // three plain Unary calls and needs nothing from the identity provider.
        while (status.State == MatchQueueState.Queued)
        {
            try
            {
                await Task.Delay(_pollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await matchmaking.CancelAsync();
                AnsiConsole.MarkupLine("[yellow]Left the queue.[/]");
                return null;
            }

            status = await matchmaking.GetStatusAsync();
        }

        if (status.State == MatchQueueState.None)
        {
            AnsiConsole.MarkupLine("[yellow]The queue let you go without a match. Try again.[/]");
            return null;
        }

        return status.Ticket;
    }

    private static async Task PlayAsync(MatchTicket ticket, Metadata authorization, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLineInterpolated($"[green]Matched into {ticket.RoomId} on {ticket.Endpoint}.[/]");

        using var roomChannel = GrpcChannel.ForAddress(ticket.Endpoint);
        var receiver = new DuelReceiver();

        var hub = await StreamingHubClient.ConnectAsync<IDuelHub, IDuelHubReceiver>(
            roomChannel,
            receiver,
            option: new CallOptions(authorization),
            cancellationToken: cancellationToken);

        try
        {
            await PlayAsync(hub, receiver, ticket, cancellationToken);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    private static async Task PlayAsync(
        IDuelHub hub,
        DuelReceiver receiver,
        MatchTicket ticket,
        CancellationToken cancellationToken)
    {
        var join = await hub.JoinAsync(new JoinRoomRequest(ticket.RoomId, ticket.EntryToken));
        if (join is not { Accepted: true, PlayerIndex: { } seat })
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Room {ticket.RoomId} would not seat you.[/]");
            return;
        }

        AnsiConsole.MarkupLineInterpolated(
            $"[green]Seated as player {seat}.[/] {(join.WaitingForOpponent ? "Waiting for the opponent..." : string.Empty)}");

        // The room gives up on a missing opponent by itself, so this wait can end with a start or
        // with the aborted match's result.
        Task outcome;
        try
        {
            outcome = await Task.WhenAny(receiver.Starting, receiver.Finished)
                .WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
        }
        catch (TimeoutException)
        {
            AnsiConsole.MarkupLine("[yellow]The opponent never arrived.[/]");
            return;
        }

        if (outcome == receiver.Starting)
        {
            receiver.ApplySnapshot(await hub.RequestSnapshotAsync());

            while (!receiver.Finished.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                await hub.SendInputsAsync(NextInputs());
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }

        var result = await receiver.Finished.WaitAsync(cancellationToken);
        var verdict = result.Reason switch
        {
            MatchEndReason.Aborted => "Match abandoned",
            _ => result.WinnerPlayerIndex switch
            {
                null => "Draw",
                { } winner when winner == seat => "You win",
                _ => "You lose",
            },
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
