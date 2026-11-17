using NexusExtract.Infrastructure;
using NexusExtract.Infrastructure.Services;

var builder = Host.CreateApplicationBuilder(args);

// Register Core Infrastructure & Application services
builder.Services.AddNexusInfrastructure(builder.Configuration);

// Register Background Processing Worker
builder.Services.AddHostedService<ExtractionWorkerService>();

var host = builder.Build();
host.Run();
