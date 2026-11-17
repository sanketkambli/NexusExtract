using NexusExtract.Domain.Entities;

namespace NexusExtract.Domain.Interfaces;

public interface IAiSchemaMappingService
{
    Task<AiMappingResult> SuggestMappingsAsync(
        string partnerName,
        List<string> sourceColumns,
        List<Dictionary<string, string>> sampleRows,
        string correlationId,
        CancellationToken ct = default);
}

public sealed record AiMappingResult(
    List<FieldMappingRule> SuggestedMappings,
    string ModelUsed,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd,
    double ProcessingTimeMs,
    string RagContextSummary
);

public interface IRagKnowledgeBase
{
    IReadOnlyList<CanonicalFieldDefinition> GetCanonicalSchema();
    Task<string> RetrieveRelevantContextAsync(List<string> sourceColumns, CancellationToken ct = default);
}
