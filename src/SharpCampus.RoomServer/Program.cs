using Cysharp.Threading;
using Microsoft.Extensions.Options;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.RoomServer.Hubs;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.RoomServer.Observability;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Server.Common.Logging;
using SharpCampus.Server.Common.MasterData;
using SharpCampus.Server.Common.Observability;
using SharpCampus.Server.Common.Services;
using SharpCampus.Server.Common.Settlement;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSharpCampusLogging();
builder.Services.AddServerOptions(builder.Configuration);
builder.Services.AddSupabaseJwtAuthentication(builder.Configuration);
builder.Services.AddRedis(builder.Configuration);

// Settlement is written from here rather than handed to the ApiServer over an internal call.
// Production note: a real service would think harder about which process owns writes to which table.
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddEntryTokens(builder.Configuration);
builder.Services.AddMasterData(builder.Configuration);
builder.Services.AddSharpCampusObservability(
    builder.Configuration,
    RoomMetrics.MeterName,
    RoomMetrics.ConfigureViews,
    checks => checks.AddRedisCheck().AddDatabaseCheck());

builder.Services.AddSingleton(provider => SimulationConfigFactory.Create(provider.GetRequiredService<MemoryDatabase>()));

builder.Services.AddOptions<RoomServerOptions>()
    .Bind(builder.Configuration.GetSection(RoomServerOptions.SectionName))
    .PostConfigure<IOptions<ServerOptions>>((room, server) =>
    {
        if (string.IsNullOrEmpty(room.Name))
        {
            room.Name = server.Value.Name;
        }
    });

// One thread per core runs every room in this process: a room is a loop action, not a thread.
builder.Services.AddSingleton<ILogicLooperPool>(provider => new LogicLooperPool(
    provider.GetRequiredService<DuelRules>().TickRate,
    Environment.ProcessorCount,
    RoundRobinLogicLooperPoolBalancer.Instance));
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<RoomMetrics>();
builder.Services.AddHostedService<RoomRegistryHeartbeat>();

// Nothing binds HostOptions from configuration on its own, and the drain wait in StoppingAsync runs
// under its ShutdownTimeout.
builder.Services.Configure<HostOptions>(builder.Configuration.GetSection("HostOptions"));
builder.Services.AddHostedService<RoomDrainService>();

// The tick loop only decides who won; what a win is worth happens on the other side of this.
builder.Services.AddMessagePipe();
builder.Services.AddScoped<IMatchSettlementService, MatchSettlementService>();
builder.Services.AddSingleton<MatchSettlementHandler>();
builder.Services.AddHostedService<MatchSettlementSubscription>();

// The same publication reaches a second subscriber that knows nothing about the first.
builder.Services.AddSingleton<MissionProgressHandler>();
builder.Services.AddHostedService<MissionProgressSubscription>();
builder.Services.AddMagicOnion(options =>
{
    options.MessageSerializer = ContractSerialization.Provider;

    // A connection that dies silently has to be noticed well inside the room's disconnect grace, or the
    // seat would sit there until the operating system gives up on the socket.
    options.EnableStreamingHubHeartbeat = true;
    options.StreamingHubHeartbeatInterval = TimeSpan.FromSeconds(1);
    options.StreamingHubHeartbeatTimeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// StatusService lives in SharpCampus.Server.Common, which the default entry-assembly scan would not reach.
app.MapMagicOnionService([typeof(StatusService), typeof(DuelHub), typeof(RoomControlService)]);

app.MapSharpCampusObservability();

// Resolved here rather than by the first caller that needs them: a gauge exists only once its owner does,
// and an idle server is exactly when a zero on /metrics is worth reading.
var rooms = app.Services.GetRequiredService<RoomManager>();
app.Services.GetRequiredService<RoomMetrics>().TrackRooms(() => rooms.RoomCount);

app.Logger.ServerStarted(app.Services.GetRequiredService<IOptions<ServerOptions>>().Value.Name, ServerVersion.Current);
app.Logger.LogMasterData(app.Services.GetRequiredService<MemoryDatabase>());

app.Run();

public partial class Program;
