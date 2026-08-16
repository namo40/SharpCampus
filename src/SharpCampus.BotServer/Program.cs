using MagicOnion.Serialization;
using Microsoft.Extensions.Options;
using SharpCampus.BotServer.Bots;
using SharpCampus.BotServer.Configuration;
using SharpCampus.BotServer.Observability;
using SharpCampus.Server.Common;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Server.Common.Logging;
using SharpCampus.Server.Common.Observability;
using SharpCampus.Shared.Serialization;

// A bot is a client of the other two servers as much as this process is a server, and every client it
// creates has to read the same MessagePack shapes those servers write.
MagicOnionSerializerProvider.Default = ContractSerialization.Provider;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSharpCampusLogging();
builder.Services.AddServerOptions(builder.Configuration);
builder.Services.Configure<BotServerOptions>(builder.Configuration.GetSection(BotServerOptions.SectionName));

// No readiness check of its own: everything this server depends on it reaches through a summon, and a
// summon that fails is already absorbed by the cooldown the ApiServer retries behind.
builder.Services.AddSharpCampusObservability(builder.Configuration, BotMetrics.MeterName);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBotAuthClient, SupabaseBotAuthClient>();
builder.Services.AddSingleton<IBotProfileClient, ApiBotProfileClient>();
builder.Services.AddSingleton<BotAccountPool>();
builder.Services.AddSingleton<BotMetrics>();
builder.Services.AddSingleton<IBotRunner, BotPlayer>();

builder.Services.AddMagicOnion(options => options.MessageSerializer = ContractSerialization.Provider);

var app = builder.Build();

// The only thing this server exposes. No game client ever talks to it, and no account of a real
// player is reachable through it.
app.MapMagicOnionService([typeof(BotControlService)]);

app.MapSharpCampusObservability();

// Resolved here rather than by the first caller that needs it: a gauge exists only once its owner does,
// and an idle server is exactly when a zero on /metrics is worth reading.
_ = app.Services.GetRequiredService<BotMetrics>();

app.Logger.ServerStarted(app.Services.GetRequiredService<IOptions<ServerOptions>>().Value.Name, ServerVersion.Current);

app.Run();
