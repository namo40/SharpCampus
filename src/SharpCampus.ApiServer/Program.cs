using Microsoft.Extensions.Options;
using SharpCampus.Server.Common;
using SharpCampus.Server.Common.Configuration;
using SharpCampus.Server.Common.Logging;
using SharpCampus.Server.Common.Services;
using SharpCampus.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSharpCampusLogging();
builder.Services.AddServerOptions(builder.Configuration);

var magicOnion = builder.Services.AddMagicOnion();

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

// Services live in SharpCampus.Server.Common, which the default entry-assembly scan would not reach.
app.MapMagicOnionService([typeof(StatusService)]);

app.Logger.ServerStarted(app.Services.GetRequiredService<IOptions<ServerOptions>>().Value.Name, ServerVersion.Current);

app.Run();

public partial class Program;
