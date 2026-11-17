using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;

namespace NexusExtract.Application.Interfaces;

public interface IJobRepository
{
    Task<ExtractionJob?> GetByIdAsync(Guid jobId, CancellationToken ct = default);
    Task<ExtractionJob?> GetByCorrelationIdAsync(string correlationId, CancellationToken ct = default);
    Task<IReadOnlyList<ExtractionJob>> GetRecentJobsAsync(int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<ExtractionJob>> GetActiveJobsAsync(CancellationToken ct = default);
    Task AddJobAsync(ExtractionJob job, CancellationToken ct = default);
    Task UpdateJobAsync(ExtractionJob job, CancellationToken ct = default);
    Task AppendLogAsync(JobExecutionLog log, CancellationToken ct = default);
    Task<IReadOnlyList<JobExecutionLog>> GetLogsForJobAsync(Guid jobId, CancellationToken ct = default);
    Task<IReadOnlyList<DataSourceDefinition>> GetAllSourcesAsync(CancellationToken ct = default);
    Task<DataSourceDefinition?> GetSourceByIdAsync(string id, CancellationToken ct = default);
    Task SaveSourceAsync(DataSourceDefinition source, CancellationToken ct = default);
}
