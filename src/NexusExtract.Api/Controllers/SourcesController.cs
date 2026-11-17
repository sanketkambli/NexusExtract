using Microsoft.AspNetCore.Mvc;
using NexusExtract.Application.Contracts.DTOs;
using NexusExtract.Application.Features.Sources;
using NexusExtract.Domain.Common;

namespace NexusExtract.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public sealed class SourcesController : ControllerBase
{
    private readonly SourceManagementService _sourceService;

    public SourcesController(SourceManagementService sourceService)
    {
        _sourceService = sourceService;
    }

    /// <summary>
    /// Lists all configured data sources and their health statuses.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DataSourceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSources(CancellationToken ct)
    {
        var sources = await _sourceService.ListSourcesAsync(ct);
        return Ok(sources);
    }

    /// <summary>
    /// Probes live connection health and round-trip latency for a given data source.
    /// </summary>
    [HttpPost("{id}/test-connection")]
    [ProducesResponseType(typeof(ConnectionHealthResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestConnection(string id, CancellationToken ct)
    {
        var result = await _sourceService.TestSourceConnectionAsync(id, ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }
}
