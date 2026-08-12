using Microsoft.Extensions.Options;
using SharpCampus.ApiServer.Matchmaking;
using SharpCampus.Server.Common;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Server.Common.Logging;
using SharpCampus.Server.Common.MasterData;
using SharpCampus.Server.Common.Services;
using SharpCampus.Shared.MasterData;
using SharpCampus.Shared.Serialization;
using SharpCampus.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSharpCampusLogging();
builder.Services.AddServerOptions(builder.Configuration);
builder.Services.AddSupabaseJwtAuthentication(builder.Configuration);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddRedis(builder.Configuration);
builder.Services.AddEntryTokens(builder.Configuration);
builder.Services.AddMasterData(builder.Configuration);

builder.Services.AddSingleton<IMatchQueue, RedisMatchQueue>();
builder.Services.AddSingleton<ITicketStore, RedisTicketStore>();
builder.Services.AddSingleton<IPairingLock, RedisPairingLock>();
builder.Services.AddSingleton<IRoomControlClient, RoomControlClient>();
builder.Services.AddHostedService<MatchmakingWorker>();

var magicOnion = builder.Services.AddMagicOnion(options => options.MessageSerializer = ContractSerialization.Provider);

// JSON transcoding is a development aid for calling Unary services over HTTP/1 (curl, Swagger UI).
if (builder.Environment.IsDevelopment())
{
    magicOnion.AddJsonTranscoding();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddMagicOnionJsonTranscodingSwagger();
    builder.Services.AddSwaggerGen(options => options.IncludeMagicOnionXmlComments(typeof(IStatusService).Assembly));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Services live in SharpCampus.Server.Common, which the default entry-assembly scan would not reach.
app.MapMagicOnionService([
    typeof(StatusService),
    typeof(AccountService),
    typeof(ProfileService),
    typeof(MatchmakingService),
]);

app.Logger.ServerStarted(app.Services.GetRequiredService<IOptions<ServerOptions>>().Value.Name, ServerVersion.Current);
app.Logger.LogMasterData(app.Services.GetRequiredService<MemoryDatabase>());

app.Run();

public partial class Program;
