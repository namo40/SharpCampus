using DFrame;
using MagicOnion.Serialization;
using SharpCampus.BotServer.Bots;
using SharpCampus.BotServer.Configuration;
using SharpCampus.Shared.Serialization;

// Every workload here is a client of the two servers, and every client it creates has to read the same
// MessagePack shapes those servers write. DFrame's own controller-to-worker channel carries a serializer
// of its own, so this does not disturb it.
MagicOnionSerializerProvider.Default = ContractSerialization.Provider;

// Web UI on the first port, workers connect on the second.
var builder = DFrameApp.CreateBuilder(7312, 7313);

builder.ConfigureServices(services =>
{
    services.Configure<BotServerOptions>(options =>
    {
        // A pool of this harness's own. Nicknames are unique across accounts, so virtual players cannot
        // wear the names the bot server's accounts already hold.
        options.EmailPattern = "loadtest{0}@sharpcampus.dev";
        options.NicknamePattern = "load{0}";
        options.Password = "sharpcampus-loadtest";

        // The ceiling on concurrency: a virtual player holds one account for as long as it plays.
        options.AccountPoolSize = 32;
    });

    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<IBotAuthClient, SupabaseBotAuthClient>();
    services.AddSingleton<IBotProfileClient, ApiBotProfileClient>();
    services.AddSingleton<BotAccountPool>();
    services.AddSingleton<IBotRunner, BotPlayer>();
});

await builder.RunAsync();
