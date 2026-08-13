using Cysharp.Threading;
using Microsoft.Extensions.Options;
using SharpCampus.RoomServer.Configuration;
using SharpCampus.RoomServer.Hubs;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Server.Common.Logging;
using SharpCampus.Server.Common.MasterData;
using SharpCampus.Server.Common.Services;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSharpCampusLogging();
builder.Services.AddServerOptions(builder.Configuration);
builder.Services.AddSupabaseJwtAuthentication(builder.Configuration);
builder.Services.AddRedis(builder.Configuration);
builder.Services.AddEntryTokens(builder.Configuration);
builder.Services.AddMasterData(builder.Configuration);
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
builder.Services.AddHostedService<RoomRegistryHeartbeat>();
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

app.Logger.ServerStarted(app.Services.GetRequiredService<IOptions<ServerOptions>>().Value.Name, ServerVersion.Current);
app.Logger.LogMasterData(app.Services.GetRequiredService<MemoryDatabase>());

app.Run();

public partial class Program;
