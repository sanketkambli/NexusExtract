using NexusExtract.Application.Common;
using NexusExtract.Application.Contracts.Requests;
using NexusExtract.Application.Contracts.Responses;
using NexusExtract.Application.Interfaces;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;
using NexusExtract.Domain.Observability;

namespace NexusExtract.Application.Features.Extractions;

public sealed class ExtractionService
{
    private readonly IJobRepository _jobRepository;
    private readonly IMessageBus _messageBus;
    private readonly IDiagnosticService _diagnosticService;

    public ExtractionService(
        IJobRepository jobRepository,
        IMessageBus messageBus,
        IDiagnosticService diagnosticService)
    {
        _jobRepository = jobRepository;
        _messageBus = messageBus;
        _diagnosticService = diagnosticService;
    }

    public async Task<Result<JobStatusResponse>> TriggerExtractionAsync(TriggerExtractionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.JobName))
        {
            return Result<JobStatusResponse>.Failure("JobName cannot be empty.");
        }

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) 
            ? Guid.NewGuid().ToString("N") 
            : request.CorrelationId;

        using var activity = NexusObservability.ActivitySource.StartActivity("TriggerExtraction");
        activity?.SetTag("job.name", request.JobName);
        activity?.SetTag("job.source", request.SourceType.ToString());
        activity?.SetTag("job.sink", request.SinkType.ToString());
        activity?.SetTag("correlation.id", correlationId);

        var job = ExtractionJob.Create(
            request.JobName,
            correlationId,
            request.SourceType,
            request.SinkType,
            request.SourceConfigJson,
            request.SinkConfigJson,
            request.InitiatedBy);

        job.MarkQueued();

        await _jobRepository.AddJobAsync(job, ct);

        var initialLog = JobExecutionLog.Create(
            job.Id,
            job.CorrelationId,
            "Queued",
            $"Job '{job.JobName}' queued for processing from {job.SourceType} to {job.SinkType}.",
            "Info");

        await _jobRepository.AppendLogAsync(initialLog, ct);

        NexusObservability.JobsStartedCounter.Add(1, 
            new KeyValuePair<string, object?>("source", job.SourceType.ToString()),
            new KeyValuePair<string, object?>("sink", job.SinkType.ToString()));
        NexusObservability.ActiveJobsCounter.Add(1);

        // Publish to Event Bus (Kafka / GCP Pub/Sub / In-Memory Channel)
        await _messageBus.PublishAsync("extraction-jobs", new ExtractionJobQueuedMessage(job.Id, job.CorrelationId), job.CorrelationId, ct);

        return Result<JobStatusResponse>.Success(MapToResponse(job));
    }

    public async Task<Result<JobStatusResponse>> ReplayJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, ct);
        if (job is null)
        {
            return Result<JobStatusResponse>.Failure($"Job with ID '{jobId}' was not found.");
        }

        if (job.Status != JobStatus.Failed && job.Status != JobStatus.DeadLettered && job.Status != JobStatus.Cancelled)
        {
            return Result<JobStatusResponse>.Failure($"Only Failed, DeadLettered, or Cancelled jobs can be replayed. Current status: {job.Status}");
        }

        var replayCorrelationId = $"replay-{Guid.NewGuid():N}";
        job.ResetForReplay(replayCorrelationId);

        await _jobRepository.UpdateJobAsync(job, ct);

        var replayLog = JobExecutionLog.Create(
            job.Id,
            job.CorrelationId,
            "ReplayInitiated",
            $"Job '{job.JobName}' re-queued via Message Replay mechanism with Correlation ID {replayCorrelationId}.",
            "Info");

        await _jobRepository.AppendLogAsync(replayLog, ct);

        NexusObservability.ActiveJobsCounter.Add(1);

        // Re-publish to Message Bus
        await _messageBus.PublishAsync("extraction-jobs", new ExtractionJobQueuedMessage(job.Id, job.CorrelationId), job.CorrelationId, ct);

        return Result<JobStatusResponse>.Success(MapToResponse(job));
    }

    public async Task<Result<JobStatusResponse>> GetJobStatusAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, ct);
        if (job is null)
        {
            return Result<JobStatusResponse>.Failure($"Job with ID '{jobId}' was not found.");
        }

        return Result<JobStatusResponse>.Success(MapToResponse(job));
    }

    public async Task<Result<JobDiagnosticResponse>> GetJobDiagnosticsAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, ct);
        if (job is null)
        {
            return Result<JobDiagnosticResponse>.Failure($"Job with ID '{jobId}' was not found.");
        }

        var logs = await _jobRepository.GetLogsForJobAsync(jobId, ct);
        var diagnostic = _diagnosticService.AnalyzeJob(job, logs);

        return Result<JobDiagnosticResponse>.Success(diagnostic);
    }

    public async Task<IReadOnlyList<JobStatusResponse>> ListRecentJobsAsync(int limit = 50, CancellationToken ct = default)
    {
        var jobs = await _jobRepository.GetRecentJobsAsync(limit, ct);
        return jobs.Select(MapToResponse).ToList();
    }

    public async Task<Result> CancelJobAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _jobRepository.GetByIdAsync(jobId, ct);
        if (job is null)
        {
            return Result.Failure($"Job '{jobId}' not found.");
        }

        if (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed || job.Status == JobStatus.DeadLettered)
        {
            return Result.Failure($"Cannot cancel job '{jobId}' in status '{job.Status}'.");
        }

        job.MarkCancelled();
        NexusObservability.ActiveJobsCounter.Add(-1);
        await _jobRepository.UpdateJobAsync(job, ct);
        await _jobRepository.AppendLogAsync(
            JobExecutionLog.Create(job.Id, job.CorrelationId, "Cancellation", "Job cancelled by operator request.", "Warn"), 
            ct);

        return Result.Success();
    }

    private static JobStatusResponse MapToResponse(ExtractionJob job)
    {
        return new JobStatusResponse(
            job.Id,
            job.JobName,
            job.CorrelationId,
            job.SourceType,
            job.SinkType,
            job.Status,
            job.BytesExtracted,
            job.RowsExtracted,
            job.TargetOutputPath,
            job.ChecksumSha256,
            job.ErrorMessage,
            job.RetryCount,
            job.MaxRetries,
            job.CreatedAtUtc,
            job.StartedAtUtc,
            job.CompletedAtUtc,
            job.InitiatedBy
        );
    }
}

public sealed record ExtractionJobQueuedMessage(Guid JobId, string CorrelationId);
