using System.Collections.Concurrent;
using NexusExtract.Application.Common;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Application.Features.SchemaMapping;

public sealed record AnalyzeSchemaRequest(
    string PartnerName,
    List<string> SourceColumns,
    List<Dictionary<string, string>>? SampleRows,
    string? CorrelationId
);

public sealed record AnalyzeSchemaResponse(
    Guid SessionId,
    string PartnerName,
    string CorrelationId,
    List<FieldMappingRule> SuggestedMappings,
    string ModelUsed,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd,
    double ProcessingTimeMs,
    string RagContextSummary
);

public sealed record ApproveSchemaRequest(
    List<FieldMappingRule> FinalMappings,
    string ApprovedBy
);

public sealed class SchemaMappingService
{
    private readonly IAiSchemaMappingService _aiService;
    private readonly IRagKnowledgeBase _ragKnowledgeBase;
    // Fast concurrent store for active mapping sessions
    private static readonly ConcurrentDictionary<Guid, SchemaMappingSession> Sessions = new();

    public SchemaMappingService(
        IAiSchemaMappingService aiService,
        IRagKnowledgeBase ragKnowledgeBase)
    {
        _aiService = aiService;
        _ragKnowledgeBase = ragKnowledgeBase;
    }

    public async Task<Result<AnalyzeSchemaResponse>> AnalyzeAndMapAsync(
        AnalyzeSchemaRequest request,
        CancellationToken ct = default)
    {
        if (request.SourceColumns == null || request.SourceColumns.Count == 0)
        {
            return Result<AnalyzeSchemaResponse>.Failure("At least one source column must be provided for AI schema analysis.");
        }

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) 
            ? Guid.NewGuid().ToString("N") 
            : request.CorrelationId;

        var sampleRows = request.SampleRows ?? new List<Dictionary<string, string>>();

        var aiResult = await _aiService.SuggestMappingsAsync(
            request.PartnerName,
            request.SourceColumns,
            sampleRows,
            correlationId,
            ct);

        var session = SchemaMappingSession.Create(
            request.PartnerName,
            correlationId,
            aiResult.ModelUsed,
            aiResult.SuggestedMappings,
            aiResult.PromptTokens,
            aiResult.CompletionTokens,
            aiResult.EstimatedCostUsd,
            aiResult.ProcessingTimeMs);

        Sessions[session.Id] = session;

        var response = new AnalyzeSchemaResponse(
            session.Id,
            session.PartnerName,
            session.CorrelationId,
            session.Mappings,
            session.AiModelUsed,
            session.PromptTokens,
            session.CompletionTokens,
            session.EstimatedCostUsd,
            session.ProcessingTimeMs,
            aiResult.RagContextSummary
        );

        return Result<AnalyzeSchemaResponse>.Success(response);
    }

    public Task<Result<SchemaMappingSession>> ApproveSessionAsync(
        Guid sessionId,
        ApproveSchemaRequest request,
        CancellationToken ct = default)
    {
        if (!Sessions.TryGetValue(sessionId, out var session))
        {
            return Task.FromResult(Result<SchemaMappingSession>.Failure($"Schema mapping session '{sessionId}' not found."));
        }

        session.Approve(request.FinalMappings, request.ApprovedBy ?? "SystemOperator");
        return Task.FromResult(Result<SchemaMappingSession>.Success(session));
    }

    public Task<Result<IReadOnlyList<CanonicalFieldDefinition>>> GetCanonicalSchemaAsync()
    {
        return Task.FromResult(Result<IReadOnlyList<CanonicalFieldDefinition>>.Success(_ragKnowledgeBase.GetCanonicalSchema()));
    }

    public Task<Result<IReadOnlyList<SchemaMappingSession>>> GetRecentSessionsAsync()
    {
        var list = Sessions.Values.OrderByDescending(s => s.CreatedAtUtc).Take(20).ToList();
        return Task.FromResult(Result<IReadOnlyList<SchemaMappingSession>>.Success(list));
    }
}
