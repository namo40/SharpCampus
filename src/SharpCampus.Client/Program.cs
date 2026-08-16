using System.Text;
using MagicOnion.Serialization;
using SharpCampus.Client;
using SharpCampus.Client.Common;
using SharpCampus.Client.Resources;
using SharpCampus.Shared.Serialization;

// Every client and hub this process creates has to read the same MessagePack shapes the servers write.
MagicOnionSerializerProvider.Default = ContractSerialization.Provider;

// Also the first touch of Client.Common, whose module initializer registers the generated client
// factories. A client created before it would fall back to reflection without saying so.
Localization.Apply(args);

// The menus are drawn in the language the launcher asked for, which the console's own code page rarely covers.
Console.OutputEncoding = Encoding.UTF8;

// Selection prompts and the duel's key reader both need a console behind the handles rather than a pipe.
if (Console.IsInputRedirected || Console.IsOutputRedirected)
{
    Console.Out.WriteLine(Strings.InteractiveTerminalRequired);
    return;
}

await new App().RunAsync();
