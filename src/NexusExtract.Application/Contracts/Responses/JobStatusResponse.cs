using NexusExtract.Domain.Enums;

namespace NexusExtract.Application.Contracts.Responses;

public sealed record JobStatusResponse(
    Guid JobId,
    string JobName,
    string CorrelationId,
    SourceType SourceType,
    SinkType SinkType,
    JobStatus Status,
    long BytesExtracted,
    long RowsExtracted,
    string? TargetOutputPath,
    string? ChecksumSha256,
    string? ErrorMessage,
    int RetryCount,
    int MaxRetries,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string InitiatedBy
);
