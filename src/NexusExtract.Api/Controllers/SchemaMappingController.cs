using Microsoft.AspNetCore.Mvc;
using NexusExtract.Application.Features.SchemaMapping;
using NexusExtract.Domain.Entities;

namespace NexusExtract.Api.Controllers;

[ApiController]
[Route("api/v1/schema-mapping")]
[Produces("application/json")]
public sealed class SchemaMappingController : ControllerBase
{
    private readonly SchemaMappingService _schemaMappingService;

    public SchemaMappingController(SchemaMappingService schemaMappingService)
    {
        _schemaMappingService = schemaMappingService;
    }

    /// <summary>
    /// Analyzes an incoming supplier schema using Gemini AI and internal RAG canonical knowledge base.
    /// </summary>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AnalyzeSchemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AnalyzeSchema(
        [FromBody] AnalyzeSchemaRequest request,
        CancellationToken ct)
    {
        var result = await _schemaMappingService.AnalyzeAndMapAsync(request, ct);
        if (!result.IsSuccess)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Human-in-the-loop approval: Commits and approves field mappings for automated extraction ingestion.
    /// </summary>
    [HttpPost("{sessionId:guid}/approve")]
    [ProducesResponseType(typeof(SchemaMappingSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveSchema(
        [FromRoute] Guid sessionId,
        [FromBody] ApproveSchemaRequest request,
        CancellationToken ct)
    {
        var result = await _schemaMappingService.ApproveSessionAsync(sessionId, request, ct);
        if (!result.IsSuccess)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Returns the enterprise canonical product schema definitions and synonym mappings.
    /// </summary>
    [HttpGet("canonical-schema")]
    [ProducesResponseType(typeof(IReadOnlyList<CanonicalFieldDefinition>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCanonicalSchema()
    {
        var result = await _schemaMappingService.GetCanonicalSchemaAsync();
        return Ok(result.Value);
    }

    /// <summary>
    /// Returns recent schema mapping sessions and approval audits.
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<SchemaMappingSession>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentSessions()
    {
        var result = await _schemaMappingService.GetRecentSessionsAsync();
        return Ok(result.Value);
    }
}
