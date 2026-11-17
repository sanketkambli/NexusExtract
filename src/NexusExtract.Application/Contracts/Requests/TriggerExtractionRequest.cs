using NexusExtract.Domain.Enums;

namespace NexusExtract.Application.Contracts.Requests;

public sealed class TriggerExtractionRequest
{
    public required string JobName { get; init; }
    public SourceType SourceType { get; init; }
    public SinkType SinkType { get; init; }
    public required string SourceConfigJson { get; init; }
    public required string SinkConfigJson { get; init; }
    public string? CorrelationId { get; init; }
    public string InitiatedBy { get; init; } = "BusinessUser";
}
