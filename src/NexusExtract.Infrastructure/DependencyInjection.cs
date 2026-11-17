using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using NexusExtract.Adapters;
using NexusExtract.Adapters.Geneva;
using NexusExtract.Adapters.GoTrade;
using NexusExtract.Adapters.RestApi;
using NexusExtract.Adapters.SftpDrop;
using NexusExtract.Application.Features.Extractions;
using NexusExtract.Application.Features.SchemaMapping;
using NexusExtract.Application.Features.Sources;
using NexusExtract.Application.Interfaces;
using NexusExtract.Application.Services;
using NexusExtract.Domain.Interfaces;
using NexusExtract.Domain.Observability;
using NexusExtract.Infrastructure.Ai;
using NexusExtract.Infrastructure.Messaging;
using NexusExtract.Infrastructure.Persistence;
using NexusExtract.Infrastructure.Sinks;

namespace NexusExtract.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNexusInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Persistence / Dapper
        var dbConfig = new DatabaseConfig
        {
            Provider = configuration["Database:Provider"] ?? "Sqlite",
            ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=nexus_extract.db"
        };
        services.AddSingleton(dbConfig);
        services.AddSingleton<IJobRepository, JobRepository>();

        // 2. Messaging Provider Selection: "PubSub" (GCP), "Kafka", or "InMemory"
        var messagingProvider = configuration["Messaging:Provider"] ?? (configuration.GetValue<bool>("Messaging:UseKafka", false) ? "Kafka" : "InMemory");

        if (messagingProvider.Equals("PubSub", StringComparison.OrdinalIgnoreCase))
        {
            var gcpConfig = new GcpPubSubConfig
            {
                ProjectId = configuration["Gcp:ProjectId"] ?? "nexus-data-lake-prod"
            };
            services.AddSingleton(gcpConfig);
            services.AddSingleton<IMessageBus, GcpPubSubMessageBus>();
        }
        else if (messagingProvider.Equals("Kafka", StringComparison.OrdinalIgnoreCase))
        {
            var kafkaConfig = new KafkaConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "127.0.0.1:9092",
                GroupId = configuration["Kafka:GroupId"] ?? "nexus-extract-group"
            };
            services.AddSingleton(kafkaConfig);
            services.AddSingleton<IMessageBus, KafkaMessageBus>();
        }
        else
        {
            services.AddSingleton<IMessageBus, InMemoryMessageBus>();
        }

        // 3. Pluggable Adapters
        services.AddHttpClient<GenericRestAdapter>();
        services.AddSingleton<IDataSourceAdapter, GenevaSocketAdapter>();
        services.AddSingleton<IDataSourceAdapter, GoTradeSybaseAdapter>();
        services.AddSingleton<IDataSourceAdapter, PartnerSftpAdapter>();
        services.AddSingleton<IDataSourceAdapter, GenericRestAdapter>();
        services.AddSingleton<IDataSourceAdapterFactory, DataSourceAdapterFactory>();

        // 4. Pluggable FileSync Sinks
        services.AddSingleton<IFileSink, LocalDiskSink>();
        services.AddSingleton<IFileSink, S3MinioCloudSink>();
        services.AddSingleton<IFileSinkFactory, FileSinkFactory>();

        // 5. Application Services
        services.AddSingleton<IDiagnosticService, DiagnosticService>();
        services.AddScoped<ExtractionService>();
        services.AddScoped<SourceManagementService>();
        services.AddScoped<SchemaMappingService>();

        // 6. AI Schema Assistant & RAG Services
        services.AddSingleton<IRagKnowledgeBase, RagKnowledgeBase>();
        services.AddHttpClient<IAiSchemaMappingService, GeminiAiService>();

        // 7. OpenTelemetry Distributed Tracing & Metrics Setup
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(NexusObservability.ServiceName, serviceVersion: NexusObservability.ServiceVersion))
            .WithTracing(tracing => tracing
                .AddSource(NexusObservability.ServiceName)
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddMeter(NexusObservability.ServiceName)
                .AddConsoleExporter());

        return services;
    }
}
