using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NexusExtract.Application.Interfaces;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;

namespace NexusExtract.Infrastructure.Persistence;

public sealed class DatabaseConfig
{
    public string Provider { get; set; } = "Sqlite"; // "Sqlite" or "SqlServer"
    public string ConnectionString { get; set; } = "Data Source=nexus_extract.db";
}

public sealed class JobRepository : IJobRepository
{
    private readonly DatabaseConfig _config;
    private readonly ILogger<JobRepository> _logger;

    public JobRepository(DatabaseConfig config, ILogger<JobRepository> logger)
    {
        _config = config;
        _logger = logger;
        InitializeSchema();
    }

    private IDbConnection CreateConnection()
    {
        if (_config.Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlConnection(_config.ConnectionString);
        }
        return new SqliteConnection(_config.ConnectionString);
    }

    private void InitializeSchema()
    {
        try
        {
            using var conn = CreateConnection();
            conn.Open();

            var createJobsTable = @"
                CREATE TABLE IF NOT EXISTS ExtractionJobs (
                    Id TEXT PRIMARY KEY,
                    JobName TEXT NOT NULL,
                    CorrelationId TEXT NOT NULL,
                    SourceType INTEGER NOT NULL,
                    SinkType INTEGER NOT NULL,
                    SourceConfigJson TEXT NOT NULL,
                    SinkConfigJson TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    BytesExtracted INTEGER NOT NULL DEFAULT 0,
                    RowsExtracted INTEGER NOT NULL DEFAULT 0,
                    TargetOutputPath TEXT NULL,
                    ChecksumSha256 TEXT NULL,
                    ErrorMessage TEXT NULL,
                    ErrorDetail TEXT NULL,
                    RetryCount INTEGER NOT NULL DEFAULT 0,
                    MaxRetries INTEGER NOT NULL DEFAULT 3,
                    CreatedAtUtc TEXT NOT NULL,
                    StartedAtUtc TEXT NULL,
                    CompletedAtUtc TEXT NULL,
                    InitiatedBy TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS JobExecutionLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    JobId TEXT NOT NULL,
                    CorrelationId TEXT NOT NULL,
                    TimestampUtc TEXT NOT NULL,
                    LogLevel TEXT NOT NULL,
                    Stage TEXT NOT NULL,
                    Message TEXT NOT NULL,
                    ExceptionDetails TEXT NULL,
                    BytesTransferredSoFar INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS DataSources (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    SourceType INTEGER NOT NULL,
                    Description TEXT NOT NULL,
                    ConnectionConfigJson TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    LastHealthCheckUtc TEXT NULL,
                    HealthStatus TEXT NOT NULL DEFAULT 'Unknown',
                    LastHealthMessage TEXT NULL,
                    LastResponseTimeTicks INTEGER NULL
                );
            ";

            conn.Execute(createJobsTable);

            // Seed initial data sources if empty
            var count = conn.ExecuteScalar<int>("SELECT COUNT(1) FROM DataSources");
            if (count == 0)
            {
                SeedInitialSources(conn);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database schema with provider {Provider}", _config.Provider);
        }
    }

    private static void SeedInitialSources(IDbConnection conn)
    {
        var seedSql = @"
            INSERT INTO DataSources (Id, Name, SourceType, Description, ConnectionConfigJson, IsActive, HealthStatus)
            VALUES (@Id, @Name, @SourceType, @Description, @ConnectionConfigJson, @IsActive, @HealthStatus);
        ";

        var sources = new[]
        {
            new
            {
                Id = "geneva-prod-cluster",
                Name = "Geneva Java Sockets (NY Cluster)",
                SourceType = (int)SourceType.GenevaSocket,
                Description = "High-throughput TCP binary socket listener streaming portfolio positions and allocations.",
                ConnectionConfigJson = JsonSerializer.Serialize(new { Host = "127.0.0.1", Port = 9100, AuthToken = "GENEVA_AUTH_2026", PortfolioId = "EQUITY_LONG_SHORT" }),
                IsActive = 1,
                HealthStatus = "Healthy"
            },
            new
            {
                Id = "gotrade-sybase-core",
                Name = "GoTrade Sybase ASE Database",
                SourceType = (int)SourceType.GoTradeSybase,
                Description = "Institutional trading database containing execution history, order books, and fills.",
                ConnectionConfigJson = JsonSerializer.Serialize(new { ConnectionString = "Server=127.0.0.1,1433;Database=gotrade;User Id=sa;Password=Your_password123;TrustServerCertificate=True;", SqlQuery = "SELECT TradeId, Symbol, Account, Quantity, Price, Side, ExecutedAtUtc FROM Trades", EnableSyntheticMockFallback = true }),
                IsActive = 1,
                HealthStatus = "Healthy"
            },
            new
            {
                Id = "partner-settlement-sftp",
                Name = "Partner SFTP File-Drop Service",
                SourceType = (int)SourceType.PartnerSftpDrop,
                Description = "B2B partner settlement service: drops request file manifest into /inbound and polls /outbound for response.",
                ConnectionConfigJson = JsonSerializer.Serialize(new { Host = "127.0.0.1", Port = 2222, Username = "partner_svc", Password = "SecuredPassword!", UseLocalFileSystemEmulation = true, LocalEmulationPath = "./storage_emulator/sftp" }),
                IsActive = 1,
                HealthStatus = "Healthy"
            },
            new
            {
                Id = "market-catalog-rest",
                Name = "Market Data Product Catalogue (REST)",
                SourceType = (int)SourceType.GenericRestApi,
                Description = "External REST API with API-Key authentication and cursor-based pagination streaming product catalogue.",
                ConnectionConfigJson = JsonSerializer.Serialize(new { BaseUrl = "https://api.partnerportal.com/v1", EndpointPath = "/products/catalog", AuthType = "ApiKey", ApiKeyHeader = "X-API-KEY", ApiKeyValue = "demo_key_2026", PageSize = 500, MaxPages = 10, EnableSyntheticMockFallback = true }),
                IsActive = 1,
                HealthStatus = "Healthy"
            }
        };

        conn.Execute(seedSql, sources);
    }

    public async Task<ExtractionJob?> GetByIdAsync(Guid jobId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<JobDbModel>(
            "SELECT * FROM ExtractionJobs WHERE Id = @Id", new { Id = jobId.ToString() });
        return row != null ? MapToEntity(row) : null;
    }

    public async Task<ExtractionJob?> GetByCorrelationIdAsync(string correlationId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<JobDbModel>(
            "SELECT * FROM ExtractionJobs WHERE CorrelationId = @CorrelationId", new { CorrelationId = correlationId });
        return row != null ? MapToEntity(row) : null;
    }

    public async Task<IReadOnlyList<ExtractionJob>> GetRecentJobsAsync(int limit = 50, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<JobDbModel>(
            "SELECT * FROM ExtractionJobs ORDER BY CreatedAtUtc DESC LIMIT @Limit", new { Limit = limit });
        return rows.Select(MapToEntity).ToList();
    }

    public async Task<IReadOnlyList<ExtractionJob>> GetActiveJobsAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<JobDbModel>(
            "SELECT * FROM ExtractionJobs WHERE Status IN (0, 1, 2, 3, 4) ORDER BY CreatedAtUtc ASC");
        return rows.Select(MapToEntity).ToList();
    }

    public async Task AddJobAsync(ExtractionJob job, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = @"
            INSERT INTO ExtractionJobs (
                Id, JobName, CorrelationId, SourceType, SinkType, SourceConfigJson, SinkConfigJson,
                Status, BytesExtracted, RowsExtracted, TargetOutputPath, ChecksumSha256,
                ErrorMessage, ErrorDetail, RetryCount, MaxRetries, CreatedAtUtc, StartedAtUtc, CompletedAtUtc, InitiatedBy
            ) VALUES (
                @Id, @JobName, @CorrelationId, @SourceType, @SinkType, @SourceConfigJson, @SinkConfigJson,
                @Status, @BytesExtracted, @RowsExtracted, @TargetOutputPath, @ChecksumSha256,
                @ErrorMessage, @ErrorDetail, @RetryCount, @MaxRetries, @CreatedAtUtc, @StartedAtUtc, @CompletedAtUtc, @InitiatedBy
            );
        ";

        await conn.ExecuteAsync(sql, new
        {
            Id = job.Id.ToString(),
            job.JobName,
            job.CorrelationId,
            SourceType = (int)job.SourceType,
            SinkType = (int)job.SinkType,
            job.SourceConfigJson,
            job.SinkConfigJson,
            Status = (int)job.Status,
            job.BytesExtracted,
            job.RowsExtracted,
            job.TargetOutputPath,
            job.ChecksumSha256,
            job.ErrorMessage,
            job.ErrorDetail,
            job.RetryCount,
            job.MaxRetries,
            CreatedAtUtc = job.CreatedAtUtc.ToString("O"),
            StartedAtUtc = job.StartedAtUtc?.ToString("O"),
            CompletedAtUtc = job.CompletedAtUtc?.ToString("O"),
            job.InitiatedBy
        });
    }

    public async Task UpdateJobAsync(ExtractionJob job, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = @"
            UPDATE ExtractionJobs SET
                Status = @Status,
                BytesExtracted = @BytesExtracted,
                RowsExtracted = @RowsExtracted,
                TargetOutputPath = @TargetOutputPath,
                ChecksumSha256 = @ChecksumSha256,
                ErrorMessage = @ErrorMessage,
                ErrorDetail = @ErrorDetail,
                RetryCount = @RetryCount,
                MaxRetries = @MaxRetries,
                StartedAtUtc = @StartedAtUtc,
                CompletedAtUtc = @CompletedAtUtc
            WHERE Id = @Id;
        ";

        await conn.ExecuteAsync(sql, new
        {
            Id = job.Id.ToString(),
            Status = (int)job.Status,
            job.BytesExtracted,
            job.RowsExtracted,
            job.TargetOutputPath,
            job.ChecksumSha256,
            job.ErrorMessage,
            job.ErrorDetail,
            job.RetryCount,
            job.MaxRetries,
            StartedAtUtc = job.StartedAtUtc?.ToString("O"),
            CompletedAtUtc = job.CompletedAtUtc?.ToString("O")
        });
    }

    public async Task AppendLogAsync(JobExecutionLog log, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = @"
            INSERT INTO JobExecutionLogs (
                JobId, CorrelationId, TimestampUtc, LogLevel, Stage, Message, ExceptionDetails, BytesTransferredSoFar
            ) VALUES (
                @JobId, @CorrelationId, @TimestampUtc, @LogLevel, @Stage, @Message, @ExceptionDetails, @BytesTransferredSoFar
            );
        ";

        await conn.ExecuteAsync(sql, new
        {
            JobId = log.JobId.ToString(),
            log.CorrelationId,
            TimestampUtc = log.TimestampUtc.ToString("O"),
            log.LogLevel,
            log.Stage,
            log.Message,
            log.ExceptionDetails,
            log.BytesTransferredSoFar
        });
    }

    public async Task<IReadOnlyList<JobExecutionLog>> GetLogsForJobAsync(Guid jobId, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<LogDbModel>(
            "SELECT * FROM JobExecutionLogs WHERE JobId = @JobId ORDER BY TimestampUtc ASC",
            new { JobId = jobId.ToString() });

        return rows.Select(r => new JobExecutionLog
        {
            Id = r.Id,
            JobId = Guid.Parse(r.JobId),
            CorrelationId = r.CorrelationId,
            TimestampUtc = DateTime.Parse(r.TimestampUtc),
            LogLevel = r.LogLevel,
            Stage = r.Stage,
            Message = r.Message,
            ExceptionDetails = r.ExceptionDetails,
            BytesTransferredSoFar = r.BytesTransferredSoFar
        }).ToList();
    }

    public async Task<IReadOnlyList<DataSourceDefinition>> GetAllSourcesAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<SourceDbModel>("SELECT * FROM DataSources ORDER BY Name ASC");
        return rows.Select(r => new DataSourceDefinition
        {
            Id = r.Id,
            Name = r.Name,
            SourceType = (SourceType)r.SourceType,
            Description = r.Description,
            ConnectionConfigJson = r.ConnectionConfigJson,
            IsActive = r.IsActive == 1,
            LastHealthCheckUtc = string.IsNullOrEmpty(r.LastHealthCheckUtc) ? null : DateTime.Parse(r.LastHealthCheckUtc),
            HealthStatus = r.HealthStatus,
            LastHealthMessage = r.LastHealthMessage,
            LastResponseTime = r.LastResponseTimeTicks.HasValue ? TimeSpan.FromTicks(r.LastResponseTimeTicks.Value) : null
        }).ToList();
    }

    public async Task<DataSourceDefinition?> GetSourceByIdAsync(string id, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var r = await conn.QuerySingleOrDefaultAsync<SourceDbModel>(
            "SELECT * FROM DataSources WHERE Id = @Id", new { Id = id });

        if (r == null) return null;

        return new DataSourceDefinition
        {
            Id = r.Id,
            Name = r.Name,
            SourceType = (SourceType)r.SourceType,
            Description = r.Description,
            ConnectionConfigJson = r.ConnectionConfigJson,
            IsActive = r.IsActive == 1,
            LastHealthCheckUtc = string.IsNullOrEmpty(r.LastHealthCheckUtc) ? null : DateTime.Parse(r.LastHealthCheckUtc),
            HealthStatus = r.HealthStatus,
            LastHealthMessage = r.LastHealthMessage,
            LastResponseTime = r.LastResponseTimeTicks.HasValue ? TimeSpan.FromTicks(r.LastResponseTimeTicks.Value) : null
        };
    }

    public async Task SaveSourceAsync(DataSourceDefinition source, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var sql = @"
            UPDATE DataSources SET
                HealthStatus = @HealthStatus,
                LastHealthCheckUtc = @LastHealthCheckUtc,
                LastHealthMessage = @LastHealthMessage,
                LastResponseTimeTicks = @LastResponseTimeTicks
            WHERE Id = @Id;
        ";

        await conn.ExecuteAsync(sql, new
        {
            source.Id,
            source.HealthStatus,
            LastHealthCheckUtc = source.LastHealthCheckUtc?.ToString("O"),
            source.LastHealthMessage,
            LastResponseTimeTicks = source.LastResponseTime?.Ticks
        });
    }

    private static ExtractionJob MapToEntity(JobDbModel r)
    {
        var job = ExtractionJob.Create(
            r.JobName,
            r.CorrelationId,
            (SourceType)r.SourceType,
            (SinkType)r.SinkType,
            r.SourceConfigJson,
            r.SinkConfigJson,
            r.InitiatedBy,
            r.MaxRetries);

        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.Id))?.SetValue(job, Guid.Parse(r.Id));
        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.Status))?.SetValue(job, (JobStatus)r.Status);
        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.BytesExtracted))?.SetValue(job, r.BytesExtracted);
        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.RowsExtracted))?.SetValue(job, r.RowsExtracted);
        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.TargetOutputPath))?.SetValue(job, r.TargetOutputPath);
        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.ChecksumSha256))?.SetValue(job, r.ChecksumSha256);
        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.ErrorMessage))?.SetValue(job, r.ErrorMessage);
        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.ErrorDetail))?.SetValue(job, r.ErrorDetail);
        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.RetryCount))?.SetValue(job, r.RetryCount);
        typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.CreatedAtUtc))?.SetValue(job, DateTime.Parse(r.CreatedAtUtc));
        if (!string.IsNullOrEmpty(r.StartedAtUtc))
        {
            typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.StartedAtUtc))?.SetValue(job, DateTime.Parse(r.StartedAtUtc));
        }
        if (!string.IsNullOrEmpty(r.CompletedAtUtc))
        {
            typeof(ExtractionJob).GetProperty(nameof(ExtractionJob.CompletedAtUtc))?.SetValue(job, DateTime.Parse(r.CompletedAtUtc));
        }

        return job;
    }

    private sealed class JobDbModel
    {
        public string Id { get; set; } = string.Empty;
        public string JobName { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public int SourceType { get; set; }
        public int SinkType { get; set; }
        public string SourceConfigJson { get; set; } = "{}";
        public string SinkConfigJson { get; set; } = "{}";
        public int Status { get; set; }
        public long BytesExtracted { get; set; }
        public long RowsExtracted { get; set; }
        public string? TargetOutputPath { get; set; }
        public string? ChecksumSha256 { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorDetail { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string? StartedAtUtc { get; set; }
        public string? CompletedAtUtc { get; set; }
        public string InitiatedBy { get; set; } = string.Empty;
    }

    private sealed class LogDbModel
    {
        public long Id { get; set; }
        public string JobId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public string TimestampUtc { get; set; } = string.Empty;
        public string LogLevel { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ExceptionDetails { get; set; }
        public long BytesTransferredSoFar { get; set; }
    }

    private sealed class SourceDbModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int SourceType { get; set; }
        public string Description { get; set; } = string.Empty;
        public string ConnectionConfigJson { get; set; } = "{}";
        public int IsActive { get; set; }
        public string? LastHealthCheckUtc { get; set; }
        public string HealthStatus { get; set; } = string.Empty;
        public string? LastHealthMessage { get; set; }
        public long? LastResponseTimeTicks { get; set; }
    }
}
