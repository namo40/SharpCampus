using System.CommandLine.Parsing;
using ConsoleAppFramework;
using MagicOnion.Serialization;
using SharpCampus.Shared.Serialization;
using Spectre.Console;

// Every client and hub this process creates has to read the same MessagePack shapes the servers write.
MagicOnionSerializerProvider.Default = ContractSerialization.Provider;

// Command classes register themselves through [RegisterCommands]; nothing is added by hand here.
var app = ConsoleApp.Create();

if (args.Length > 0)
{
    await app.RunAsync(args);
    return;
}

AnsiConsole.MarkupLine("[grey]SharpCampus client. Type 'exit' to quit.[/]");

while (true)
{
    // Console.ReadLine keeps the console host's built-in line editing and history, which a Spectre prompt would replace.
    AnsiConsole.Markup("[green]cli>[/] ");

    var line = Console.ReadLine();
    if (line is null)
    {
        break;
    }

    var commandArgs = CommandLineParser.SplitCommandLine(line).ToArray();
    if (commandArgs.Length == 0)
    {
        continue;
    }

    if (string.Equals(commandArgs[0], "exit", StringComparison.OrdinalIgnoreCase)
        || string.Equals(commandArgs[0], "quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    // Keep the service provider alive across iterations: it will hold the interactive session state.
    await app.RunAsync(commandArgs, disposeServiceProvider: false);
}
