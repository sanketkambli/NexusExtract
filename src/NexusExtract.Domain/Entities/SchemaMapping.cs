namespace NexusExtract.Domain.Entities;

public sealed class CanonicalFieldDefinition
{
    public string FieldName { get; init; } = string.Empty;
    public string DataType { get; init; } = "string";
    public bool IsRequired { get; init; }
    public string Description { get; init; } = string.Empty;
    public string ExampleValue { get; init; } = string.Empty;
    public string[] KnownSynonyms { get; init; } = Array.Empty<string>();
}

public sealed class FieldMappingRule
{
    public string SourceColumn { get; set; } = string.Empty;
    public string TargetCanonicalField { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string TransformationRule { get; set; } = "Direct";
    public string AiRationale { get; set; } = string.Empty;
    public bool IsUserOverridden { get; set; }
}

public sealed class SchemaMappingSession
{
    public Guid Id { get; private set; }
    public string PartnerName { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public List<FieldMappingRule> Mappings { get; private set; } = new();
    public string Status { get; private set; } = "PendingReview"; // PendingReview, Approved, Rejected
    public string AiModelUsed { get; private set; } = "gemini-2.5-flash";
    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public double ProcessingTimeMs { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ApprovedAtUtc { get; private set; }
    public string? ApprovedBy { get; private set; }

    private SchemaMappingSession() { }

    public static SchemaMappingSession Create(
        string partnerName,
        string correlationId,
        string aiModel,
        List<FieldMappingRule> mappings,
        int promptTokens,
        int completionTokens,
        decimal estimatedCostUsd,
        double processingTimeMs)
    {
        return new SchemaMappingSession
        {
            Id = Guid.NewGuid(),
            PartnerName = partnerName,
            CorrelationId = correlationId,
            AiModelUsed = aiModel,
            Mappings = mappings,
            Status = "PendingReview",
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            EstimatedCostUsd = estimatedCostUsd,
            ProcessingTimeMs = processingTimeMs,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Approve(List<FieldMappingRule> finalMappings, string approvedBy)
    {
        Mappings = finalMappings;
        Status = "Approved";
        ApprovedAtUtc = DateTime.UtcNow;
        ApprovedBy = approvedBy;
    }

    public void Reject(string rejectedBy)
    {
        Status = "Rejected";
        ApprovedAtUtc = DateTime.UtcNow;
        ApprovedBy = rejectedBy;
    }
}
