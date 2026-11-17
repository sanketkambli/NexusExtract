using NexusExtract.Domain.Enums;

namespace NexusExtract.Domain.Entities;

public sealed class ExtractionJob
{
    public Guid Id { get; private set; }
    public string JobName { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public SourceType SourceType { get; private set; }
    public SinkType SinkType { get; private set; }
    public string SourceConfigJson { get; private set; } = "{}";
    public string SinkConfigJson { get; private set; } = "{}";
    public JobStatus Status { get; private set; } = JobStatus.Pending;
    public long BytesExtracted { get; private set; }
    public long RowsExtracted { get; private set; }
    public string? TargetOutputPath { get; private set; }
    public string? ChecksumSha256 { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ErrorDetail { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; } = 3;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string InitiatedBy { get; private set; } = "System";

    private ExtractionJob() { }

    public static ExtractionJob Create(
        string jobName,
        string correlationId,
        SourceType sourceType,
        SinkType sinkType,
        string sourceConfigJson,
        string sinkConfigJson,
        string initiatedBy = "BusinessUser",
        int maxRetries = 3)
    {
        return new ExtractionJob
        {
            Id = Guid.NewGuid(),
            JobName = jobName,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
            SourceType = sourceType,
            SinkType = sinkType,
            SourceConfigJson = sourceConfigJson,
            SinkConfigJson = sinkConfigJson,
            Status = JobStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            InitiatedBy = initiatedBy,
            MaxRetries = maxRetries,
            RetryCount = 0
        };
    }

    public void MarkQueued()
    {
        Status = JobStatus.Queued;
    }

    public void MarkStarted()
    {
        Status = JobStatus.Connecting;
        StartedAtUtc = DateTime.UtcNow;
    }

    public void UpdateProgress(long bytes, long rows, JobStatus currentStage = JobStatus.Extracting)
    {
        BytesExtracted = bytes;
        RowsExtracted = rows;
        Status = currentStage;
    }

    public void MarkCompleted(string targetOutputPath, string checksumSha256, long totalBytes, long totalRows)
    {
        Status = JobStatus.Completed;
        TargetOutputPath = targetOutputPath;
        ChecksumSha256 = checksumSha256;
        BytesExtracted = totalBytes;
        RowsExtracted = totalRows;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage, string? errorDetail = null)
    {
        Status = JobStatus.Failed;
        ErrorMessage = errorMessage;
        ErrorDetail = errorDetail;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void MarkDeadLettered(string reason, string? errorDetail = null)
    {
        Status = JobStatus.DeadLettered;
        ErrorMessage = $"[DLQ - Max Retries Exceeded] {reason}";
        ErrorDetail = errorDetail;
        CompletedAtUtc = DateTime.UtcNow;
    }

    public bool CanRetry() => RetryCount < MaxRetries;

    public void IncrementRetry()
    {
        RetryCount++;
        Status = JobStatus.Queued;
    }

    public void ResetForReplay(string newCorrelationId)
    {
        CorrelationId = string.IsNullOrWhiteSpace(newCorrelationId) ? Guid.NewGuid().ToString("N") : newCorrelationId;
        Status = JobStatus.Queued;
        RetryCount = 0;
        ErrorMessage = null;
        ErrorDetail = null;
        BytesExtracted = 0;
        RowsExtracted = 0;
        TargetOutputPath = null;
        ChecksumSha256 = null;
        StartedAtUtc = null;
        CompletedAtUtc = null;
    }

    public void MarkCancelled()
    {
        Status = JobStatus.Cancelled;
        CompletedAtUtc = DateTime.UtcNow;
    }
}
