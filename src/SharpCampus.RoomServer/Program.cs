using Microsoft.Extensions.Options;
using SharpCampus.Server.Common;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Server.Common.Logging;
using SharpCampus.Server.Common.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSharpCampusLogging();
builder.Services.AddServerOptions(builder.Configuration);
builder.Services.AddMagicOnion();

var app = builder.Build();

// Services live in SharpCampus.Server.Common, which the default entry-assembly scan would not reach.
app.MapMagicOnionService([typeof(StatusService)]);

app.Logger.ServerStarted(app.Services.GetRequiredService<IOptions<ServerOptions>>().Value.Name, ServerVersion.Current);

app.Run();

public partial class Program;
