using Moq;
using NexusExtract.Application.Features.SchemaMapping;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Interfaces;
using Xunit;

namespace NexusExtract.UnitTests;

public class SchemaMappingTests
{
    private readonly Mock<IAiSchemaMappingService> _aiServiceMock;
    private readonly Mock<IRagKnowledgeBase> _ragMock;
    private readonly SchemaMappingService _mappingService;

    public SchemaMappingTests()
    {
        _aiServiceMock = new Mock<IAiSchemaMappingService>();
        _ragMock = new Mock<IRagKnowledgeBase>();

        _ragMock.Setup(r => r.GetCanonicalSchema()).Returns(new List<CanonicalFieldDefinition>
        {
            new() { FieldName = "ManufacturerPartNumber_MPN", DataType = "string", IsRequired = true },
            new() { FieldName = "WholesalePriceUsd", DataType = "decimal", IsRequired = true },
            new() { FieldName = "AvailableQuantityOnHand", DataType = "integer", IsRequired = true },
        });

        _mappingService = new SchemaMappingService(_aiServiceMock.Object, _ragMock.Object);
    }

    [Fact]
    public async Task GetCanonicalSchema_Returns_TargetEnterpriseFields()
    {
        var result = await _mappingService.GetCanonicalSchemaAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value!.Count);
        Assert.Contains(result.Value, f => f.FieldName == "ManufacturerPartNumber_MPN");
    }

    [Fact]
    public async Task AnalyzeAndMap_Calls_AiService_And_CreatesSession()
    {
        var supplierCols = new List<string> { "VND_PART_NO", "NET_COST_USD" };

        _aiServiceMock.Setup(ai => ai.SuggestMappingsAsync(
            It.IsAny<string>(),
            It.IsAny<List<string>>(),
            It.IsAny<List<Dictionary<string, string>>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(new AiMappingResult(
            new List<FieldMappingRule>
            {
                new() { SourceColumn = "VND_PART_NO", TargetCanonicalField = "ManufacturerPartNumber_MPN", ConfidenceScore = 0.95, TransformationRule = "Trim", AiRationale = "High confidence match to MPN" },
                new() { SourceColumn = "NET_COST_USD", TargetCanonicalField = "WholesalePriceUsd", ConfidenceScore = 0.92, TransformationRule = "ParseCurrency", AiRationale = "Currency pattern detected" }
            },
            "gemini-2.5-flash",
            420,
            180,
            0.00035m,
            185.0,
            "Ingram Canonical Product Schema v4.2"
        ));

        var request = new AnalyzeSchemaRequest("Cisco B2B", supplierCols, null, "cid-test-01");
        var result = await _mappingService.AnalyzeAndMapAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Cisco B2B", result.Value!.PartnerName);
        Assert.Equal(2, result.Value.SuggestedMappings.Count);
        Assert.Equal("gemini-2.5-flash", result.Value.ModelUsed);
        Assert.Equal(0.00035m, result.Value.EstimatedCostUsd);
    }

    [Fact]
    public async Task ApproveSession_Updates_MappingRules_And_RecordsAudit()
    {
        var supplierCols = new List<string> { "VND_SKU" };

        _aiServiceMock.Setup(ai => ai.SuggestMappingsAsync(
            It.IsAny<string>(),
            It.IsAny<List<string>>(),
            It.IsAny<List<Dictionary<string, string>>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(new AiMappingResult(
            new List<FieldMappingRule>
            {
                new() { SourceColumn = "VND_SKU", TargetCanonicalField = "ManufacturerPartNumber_MPN", ConfidenceScore = 0.90, TransformationRule = "Direct", AiRationale = "Mapped to MPN" }
            },
            "gemini-2.5-flash",
            300,
            100,
            0.0002m,
            120.0,
            "RAG v4.2"
        ));

        var analyzeResult = await _mappingService.AnalyzeAndMapAsync(new AnalyzeSchemaRequest("Medpace Vendor", supplierCols, null, "cid-test-02"));
        Assert.True(analyzeResult.IsSuccess);
        Assert.NotNull(analyzeResult.Value);

        var approvedMappings = new List<FieldMappingRule>
        {
            new() { SourceColumn = "VND_SKU", TargetCanonicalField = "ManufacturerPartNumber_MPN", ConfidenceScore = 0.90, TransformationRule = "Trim", AiRationale = "Operator override: add Trim", IsUserOverridden = true }
        };

        var approveResult = await _mappingService.ApproveSessionAsync(
            analyzeResult.Value!.SessionId,
            new ApproveSchemaRequest(approvedMappings, "LeadArchitectSanket")
        );

        Assert.True(approveResult.IsSuccess);
        Assert.NotNull(approveResult.Value);
        Assert.Equal("Approved", approveResult.Value!.Status);
        Assert.Equal("LeadArchitectSanket", approveResult.Value.ApprovedBy);
        Assert.NotNull(approveResult.Value.ApprovedAtUtc);
        Assert.True(approveResult.Value.Mappings[0].IsUserOverridden);
    }
}
