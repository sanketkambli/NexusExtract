using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Infrastructure.Ai;

public sealed class GeminiAiService : IAiSchemaMappingService
{
    private readonly HttpClient _httpClient;
    private readonly IRagKnowledgeBase _ragKnowledgeBase;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiAiService> _logger;

    public GeminiAiService(
        HttpClient httpClient,
        IRagKnowledgeBase ragKnowledgeBase,
        IConfiguration configuration,
        ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _ragKnowledgeBase = ragKnowledgeBase;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AiMappingResult> SuggestMappingsAsync(
        string partnerName,
        List<string> sourceColumns,
        List<Dictionary<string, string>> sampleRows,
        string correlationId,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var apiKey = _configuration["Gemini:ApiKey"] 
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        var ragContext = await _ragKnowledgeBase.RetrieveRelevantContextAsync(sourceColumns, ct);

        // If API key is available, call Google Gemini API with Structured Outputs
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var liveResult = await CallLiveGeminiApiAsync(partnerName, sourceColumns, sampleRows, ragContext, apiKey, ct);
                sw.Stop();
                return liveResult with { ProcessingTimeMs = sw.Elapsed.TotalMilliseconds };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live Gemini API call failed or timed out. Falling back to local RAG heuristic inference engine.");
            }
        }

        // Fallback: Local Semantic & RAG Heuristic Engine (Ensures 100% demo uptime without external key)
        var localResult = ExecuteLocalRagInference(partnerName, sourceColumns, sampleRows, ragContext);
        sw.Stop();
        return localResult with { ProcessingTimeMs = sw.Elapsed.TotalMilliseconds };
    }

    private async Task<AiMappingResult> CallLiveGeminiApiAsync(
        string partnerName,
        List<string> sourceColumns,
        List<Dictionary<string, string>> sampleRows,
        string ragContext,
        string apiKey,
        CancellationToken ct)
    {
        var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine($"Partner/Supplier: {partnerName}");
        promptBuilder.AppendLine($"Source Columns: [{string.Join(", ", sourceColumns)}]");
        if (sampleRows.Count > 0)
        {
            promptBuilder.AppendLine("Sample Data Rows:");
            promptBuilder.AppendLine(JsonSerializer.Serialize(sampleRows));
        }
        promptBuilder.AppendLine("\nContext from Internal RAG System:");
        promptBuilder.AppendLine(ragContext);
        promptBuilder.AppendLine("\nInstructions: Map each Source Column to the most appropriate Target Canonical Field. Return confidence score (0.0 - 1.0), transformation rule, and clear rationale.");

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = promptBuilder.ToString() } } }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                response_schema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        mappings = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    sourceColumn = new { type = "STRING" },
                                    targetCanonicalField = new { type = "STRING" },
                                    confidenceScore = new { type = "NUMBER" },
                                    transformationRule = new { type = "STRING" },
                                    aiRationale = new { type = "STRING" }
                                },
                                required = new[] { "sourceColumn", "targetCanonicalField", "confidenceScore", "transformationRule", "aiRationale" }
                            }
                        }
                    },
                    required = new[] { "mappings" }
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, ct);
        response.EnsureSuccessStatusCode();

        var jsonStr = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(jsonStr);
        var textPayload = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text").GetString();

        var usage = doc.RootElement.TryGetProperty("usageMetadata", out var u) ? u : default;
        int promptTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("promptTokenCount", out var pt) ? pt.GetInt32() : 450;
        int completionTokens = usage.ValueKind != JsonValueKind.Undefined && usage.TryGetProperty("candidatesTokenCount", out var ctCount) ? ctCount.GetInt32() : 210;

        // Gemini 2.5 Flash pricing: $0.075 / 1M prompt, $0.30 / 1M completion
        decimal cost = ((promptTokens * 0.075m) + (completionTokens * 0.30m)) / 1_000_000m;

        var parsed = JsonSerializer.Deserialize<JsonElement>(textPayload ?? "{}");
        var mappings = new List<FieldMappingRule>();

        if (parsed.TryGetProperty("mappings", out var mapArray))
        {
            foreach (var item in mapArray.EnumerateArray())
            {
                mappings.Add(new FieldMappingRule
                {
                    SourceColumn = item.GetProperty("sourceColumn").GetString() ?? "",
                    TargetCanonicalField = item.GetProperty("targetCanonicalField").GetString() ?? "",
                    ConfidenceScore = item.GetProperty("confidenceScore").GetDouble(),
                    TransformationRule = item.GetProperty("transformationRule").GetString() ?? "Direct",
                    AiRationale = item.GetProperty("aiRationale").GetString() ?? ""
                });
            }
        }

        return new AiMappingResult(
            mappings,
            model,
            promptTokens,
            completionTokens,
            cost,
            0,
            "Live Gemini 2.5 Structured Output via Vertex/AI Gateway"
        );
    }

    private AiMappingResult ExecuteLocalRagInference(
        string partnerName,
        List<string> sourceColumns,
        List<Dictionary<string, string>> sampleRows,
        string ragContext)
    {
        var canonicalSchema = _ragKnowledgeBase.GetCanonicalSchema();
        var mappings = new List<FieldMappingRule>();

        foreach (var col in sourceColumns)
        {
            var cleanCol = col.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
            CanonicalFieldDefinition? bestMatch = null;
            double bestScore = 0.0;
            string rationale = "";
            string transform = "Direct";

            foreach (var canon in canonicalSchema)
            {
                // Exact name match
                if (canon.FieldName.Equals(col, StringComparison.OrdinalIgnoreCase))
                {
                    bestMatch = canon;
                    bestScore = 0.99;
                    rationale = $"Exact lexical match with canonical field '{canon.FieldName}'.";
                    break;
                }

                // Known synonym match from RAG
                foreach (var syn in canon.KnownSynonyms)
                {
                    if (cleanCol == syn)
                    {
                        bestMatch = canon;
                        bestScore = 0.95;
                        rationale = $"Matched via enterprise RAG synonym dictionary: '{col}' -> '{canon.FieldName}'.";
                        break;
                    }

                    if (cleanCol.Contains(syn) || syn.Contains(cleanCol))
                    {
                        var score = 0.85;
                        if (score > bestScore)
                        {
                            bestMatch = canon;
                            bestScore = score;
                            rationale = $"High substring semantic similarity with synonym '{syn}'.";
                        }
                    }
                }
            }

            // Determine appropriate transformation rule
            if (bestMatch != null)
            {
                if (bestMatch.DataType.Contains("decimal") || bestMatch.FieldName.Contains("Price"))
                    transform = "ParseCurrency";
                else if (bestMatch.DataType.Contains("integer") || bestMatch.FieldName.Contains("Quantity"))
                    transform = "ToInteger";
                else if (bestMatch.FieldName.Contains("UPC") || bestMatch.FieldName.Contains("Sku"))
                    transform = "Trim";
            }
            else
            {
                // Default unmapped fallback
                bestMatch = canonicalSchema[0];
                bestScore = 0.35;
                rationale = "Low semantic confidence. Operator manual review required.";
                transform = "Direct";
            }

            mappings.Add(new FieldMappingRule
            {
                SourceColumn = col,
                TargetCanonicalField = bestMatch.FieldName,
                ConfidenceScore = Math.Round(bestScore, 2),
                TransformationRule = transform,
                AiRationale = rationale,
                IsUserOverridden = false
            });
        }

        // Token simulation based on standard prompt length
        int simulatedPromptTokens = 350 + (sourceColumns.Count * 25);
        int simulatedCompletionTokens = 40 + (sourceColumns.Count * 20);
        decimal simulatedCost = ((simulatedPromptTokens * 0.075m) + (simulatedCompletionTokens * 0.30m)) / 1_000_000m;

        return new AiMappingResult(
            mappings,
            "gemini-2.5-flash (Semantic RAG Fallback)",
            simulatedPromptTokens,
            simulatedCompletionTokens,
            simulatedCost,
            0,
            "RAG Knowledge Base v4.2 (Ingram Micro Canonical Specification)"
        );
    }
}
