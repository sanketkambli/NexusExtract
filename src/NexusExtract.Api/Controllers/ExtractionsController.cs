using Microsoft.AspNetCore.Mvc;
using NexusExtract.Application.Contracts.Requests;
using NexusExtract.Application.Contracts.Responses;
using NexusExtract.Application.Features.Extractions;

namespace NexusExtract.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public sealed class ExtractionsController : ControllerBase
{
    private readonly ExtractionService _extractionService;
    private readonly ILogger<ExtractionsController> _logger;

    public ExtractionsController(
        ExtractionService extractionService,
        ILogger<ExtractionsController> logger)
    {
        _extractionService = extractionService;
        _logger = logger;
    }

    /// <summary>
    /// Triggers an asynchronous data extraction job.
    /// Can be invoked by the React dashboard or third-party automated systems.
    /// </summary>
    [HttpPost("jobs")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TriggerJob([FromBody] TriggerExtractionRequest request, CancellationToken ct)
    {
        // Extract correlation ID from header if present
        var correlationId = HttpContext.Items["X-Correlation-ID"] as string ?? request.CorrelationId;
        var requestWithCorr = new TriggerExtractionRequest
        {
            JobName = request.JobName,
            SourceType = request.SourceType,
            SinkType = request.SinkType,
            SourceConfigJson = request.SourceConfigJson,
            SinkConfigJson = request.SinkConfigJson,
            CorrelationId = correlationId,
            InitiatedBy = request.InitiatedBy
        };

        var result = await _extractionService.TriggerExtractionAsync(requestWithCorr, ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return AcceptedAtAction(nameof(GetJobStatus), new { id = result.Value!.JobId }, result.Value);
    }

    /// <summary>
    /// Gets the current execution status and streaming metrics for an extraction job.
    /// </summary>
    [HttpGet("jobs/{id:guid}")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobStatus(Guid id, CancellationToken ct)
    {
        var result = await _extractionService.GetJobStatusAsync(id, ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Self-service Diagnostic endpoint for Support team and 3rd-party consumers.
    /// Returns root-cause failure classification, suggested remediation, and execution stage logs.
    /// </summary>
    [HttpGet("jobs/{id:guid}/diagnostics")]
    [ProducesResponseType(typeof(JobDiagnosticResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobDiagnostics(Guid id, CancellationToken ct)
    {
        var result = await _extractionService.GetJobDiagnosticsAsync(id, ct);
        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Lists recently submitted extraction jobs.
    /// </summary>
    [HttpGet("jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<JobStatusResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListJobs([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var jobs = await _extractionService.ListRecentJobsAsync(limit, ct);
        return Ok(jobs);
    }

    /// <summary>
    /// Replays a failed, cancelled, or dead-lettered job by re-queuing it into the extraction pipeline.
    /// Used by support staff and automated recovery systems after fixing underlying issues.
    /// </summary>
    [HttpPost("jobs/{id:guid}/replay")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplayJob(Guid id, CancellationToken ct)
    {
        var result = await _extractionService.ReplayJobAsync(id, ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Cancels an in-flight or queued extraction job.
    /// </summary>
    [HttpPost("jobs/{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelJob(Guid id, CancellationToken ct)
    {
        var result = await _extractionService.CancelJobAsync(id, ct);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { message = $"Job {id} successfully cancelled." });
    }
}
