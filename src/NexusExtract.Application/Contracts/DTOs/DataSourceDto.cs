using NexusExtract.Domain.Enums;

namespace NexusExtract.Application.Contracts.DTOs;

public sealed record DataSourceDto(
    string Id,
    string Name,
    SourceType SourceType,
    string Description,
    string ConnectionConfigJson,
    bool IsActive,
    DateTime? LastHealthCheckUtc,
    string HealthStatus,
    string? LastHealthMessage,
    TimeSpan? LastResponseTime
);
