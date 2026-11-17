using NexusExtract.Domain.Enums;

namespace NexusExtract.Domain.Entities;

public sealed class DataSourceDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SourceType SourceType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ConnectionConfigJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime? LastHealthCheckUtc { get; set; }
    public string HealthStatus { get; set; } = "Unknown"; // Healthy, Degraded, Unreachable, Unknown
    public string? LastHealthMessage { get; set; }
    public TimeSpan? LastResponseTime { get; set; }
}
