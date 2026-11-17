using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;

namespace NexusExtract.Application.Contracts.Responses;

public sealed class JobDiagnosticResponse
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public SourceType SourceType { get; set; }
    public SinkType SinkType { get; set; }
    public JobStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RootCauseCategory { get; set; } // Authentication, NetworkTimeout, SourceProtocolError, SinkWriteError, DataFormatError
    public string? SuggestedRemediation { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public TimeSpan? ExecutionDuration => CompletedAtUtc.HasValue && StartedAtUtc.HasValue ? CompletedAtUtc.Value - StartedAtUtc.Value : null;
    public DateTime? StartedAtUtc { get; set; }
    public long BytesTransferred { get; set; }
    public long RowsTransferred { get; set; }
    public List<JobExecutionLog> Logs { get; set; } = [];
}
