using ConsoleAppFramework;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using SharpCampus.Cli.Resources;
using SharpCampus.Shared.Services;
using Spectre.Console;

namespace SharpCampus.Cli.Commands;

[RegisterCommands]
internal sealed class StatusCommand
{
    private static readonly (string Label, string Address)[] _servers =
    [
        ("ApiServer", ClientEndpoints.ApiServer),

        // A development convenience: a room server sits behind the entry point with no route of its own,
        // so anywhere but a local run this row is meant to read as unreachable.
        ("RoomServer", "http://localhost:5002"),
    ];

    /// <summary>Queries every SharpCampus server for its name, version and clock.</summary>
    [Command("status")]
    public async Task ExecuteAsync()
    {
        foreach (var (label, address) in _servers)
        {
            using var channel = GrpcChannel.ForAddress(address);
            var client = MagicOnionClient.Create<IStatusService>(channel);

            try
            {
                var status = await client.GetStatusAsync();

                // Interpolated markup escapes the server-supplied values, which carry markup-significant characters.
                AnsiConsole.MarkupLineInterpolated(
                    $"[green]{label}[/]: {status.ServerName} [grey]{Localization.Format(Strings.ServerVersionLine, status.Version, status.TimestampUtc)}[/]");
            }
            catch (RpcException e) when (e.StatusCode == StatusCode.Unavailable)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]{Localization.Format(Strings.ServerUnreachable, label, address)}[/]");
            }
        }
    }
}
