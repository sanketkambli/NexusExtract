using System.Text.Json.Serialization;
using NexusExtract.Api.Middleware;
using NexusExtract.Infrastructure;
using NexusExtract.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Controllers with String Enum converters for clean API contracts
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Swagger / OpenAPI documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "NexusExtract Enterprise Gateway API",
        Version = "v1",
        Description = "High-throughput, pluggable data extraction and streaming gateway supporting Geneva Sockets, Sybase, SFTP File-Drop, and REST."
    });
});

// Register Core Infrastructure (Database, Sinks, MessageBus, Pluggable Adapters, Services)
builder.Services.AddNexusInfrastructure(builder.Configuration);

// Run the Extraction Worker Host in-process for seamless standalone execution
builder.Services.AddHostedService<ExtractionWorkerService>();

// CORS configuration for React 19 Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("X-Correlation-ID");
    });
});

var app = builder.Build();

// Start Geneva Mock Socket Server on 127.0.0.1:9100 for local socket streaming demo
var genevaMock = new GenevaMockServer(port: 9100, logger: app.Services.GetService<ILogger<GenevaMockServer>>());
genevaMock.Start();
app.Lifetime.ApplicationStopping.Register(() => genevaMock.Dispose());

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "NexusExtract API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapControllers();

// Health probe
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow,
    engine = "NexusExtract Gateway (.NET 10)",
    activeSources = new[] { "GenevaSocket", "GoTradeSybase", "PartnerSftpDrop", "GenericRestApi" },
    activeSinks = new[] { "LocalDisk", "CloudStorageS3Minio", "NetworkShare" }
}));

app.Run();
