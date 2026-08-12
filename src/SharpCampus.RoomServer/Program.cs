using Cysharp.Threading;
using Microsoft.Extensions.Options;
using SharpCampus.RoomServer.Hubs;
using SharpCampus.RoomServer.MasterData;
using SharpCampus.RoomServer.Rooms;
using SharpCampus.Server.Common;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Server.Common.Logging;
using SharpCampus.Server.Common.MasterData;
using SharpCampus.Server.Common.Services;
using SharpCampus.Shared.MasterData;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSharpCampusLogging();
builder.Services.AddServerOptions(builder.Configuration);
builder.Services.AddMasterData(builder.Configuration);
builder.Services.AddSingleton(provider => SimulationConfigFactory.Create(provider.GetRequiredService<MemoryDatabase>()));

// One thread per core runs every room in this process: a room is a loop action, not a thread.
builder.Services.AddSingleton<ILogicLooperPool>(provider => new LogicLooperPool(
    provider.GetRequiredService<DuelRules>().TickRate,
    Environment.ProcessorCount,
    RoundRobinLogicLooperPoolBalancer.Instance));
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddMagicOnion();

var app = builder.Build();

// StatusService lives in SharpCampus.Server.Common, which the default entry-assembly scan would not reach.
app.MapMagicOnionService([typeof(StatusService), typeof(DuelHub)]);

app.Logger.ServerStarted(app.Services.GetRequiredService<IOptions<ServerOptions>>().Value.Name, ServerVersion.Current);
app.Logger.LogMasterData(app.Services.GetRequiredService<MemoryDatabase>());

app.Run();

public partial class Program;
