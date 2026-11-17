using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NexusExtract.Application.Features.Extractions;
using NexusExtract.Application.Interfaces;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;
using NexusExtract.Domain.Observability;

namespace NexusExtract.Infrastructure.Services;

public sealed class ExtractionWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExtractionWorkerService> _logger;

    public ExtractionWorkerService(
        IServiceProvider serviceProvider,
        ILogger<ExtractionWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExtractionWorkerService started. Subscribing to 'extraction-jobs' topic...");

        using var scope = _serviceProvider.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        await messageBus.SubscribeAsync<ExtractionJobQueuedMessage>(
            "extraction-jobs",
            async (msg, correlationId) =>
            {
                await ProcessJobAsync(msg.JobId, correlationId, stoppingToken);
            },
            stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(Guid jobId, string correlationId, CancellationToken ct)
    {
        using var activity = NexusObservability.ActivitySource.StartActivity("ProcessExtractionJob");
        activity?.SetTag("job.id", jobId.ToString());
        activity?.SetTag("correlation.id", correlationId);

        using var scope = _serviceProvider.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var adapterFactory = scope.ServiceProvider.GetRequiredService<IDataSourceAdapterFactory>();
        var sinkFactory = scope.ServiceProvider.GetRequiredService<IFileSinkFactory>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var job = await jobRepo.GetByIdAsync(jobId, ct);
        if (job is null)
        {
            _logger.LogError("Worker received unknown JobId {JobId}", jobId);
            return;
        }

        activity?.SetTag("job.name", job.JobName);
        activity?.SetTag("source.type", job.SourceType.ToString());
        activity?.SetTag("sink.type", job.SinkType.ToString());
        activity?.SetTag("retry.count", job.RetryCount);

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["JobId"] = job.Id,
            ["CorrelationId"] = correlationId,
            ["SourceType"] = job.SourceType,
            ["SinkType"] = job.SinkType,
            ["RetryCount"] = job.RetryCount
        });

        _logger.LogInformation("Worker starting extraction pipeline for Job '{JobName}' (Attempt {Attempt}/{Max})",
            job.JobName, job.RetryCount + 1, job.MaxRetries);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 1. Mark Started & Connecting
            job.MarkStarted();
            await jobRepo.UpdateJobAsync(job, ct);
            await jobRepo.AppendLogAsync(JobExecutionLog.Create(
                job.Id, correlationId, "Connecting", 
                $"Connecting to source {job.SourceType} (Attempt {job.RetryCount + 1})...", "Info"), ct);

            // 2. Resolve Adapter & Sink
            var adapterSw = Stopwatch.StartNew();
            var adapter = adapterFactory.GetAdapter(job.SourceType);
            var sink = sinkFactory.GetSink(job.SinkType);

            // 3. Mark Extracting / Streaming
            job.UpdateProgress(0, 0, JobStatus.Extracting);
            await jobRepo.UpdateJobAsync(job, ct);
            await jobRepo.AppendLogAsync(JobExecutionLog.Create(
                job.Id, correlationId, "Extracting", 
                $"Acquiring streaming source cursor from {job.SourceType}...", "Info"), ct);

            var sourceStream = adapter.ExtractStreamAsync(job, ct);
            adapterSw.Stop();
            NexusObservability.AdapterLatencyHistogram.Record(adapterSw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("source", job.SourceType.ToString()));

            // Progress throttling tracker
            var lastProgressLog = DateTime.UtcNow;
            long lastLoggedBytes = 0;

            void OnProgress(long bytes, long records)
            {
                job.UpdateProgress(bytes, records, JobStatus.StreamingToSink);
                if (DateTime.UtcNow - lastProgressLog > TimeSpan.FromSeconds(2) || bytes - lastLoggedBytes > 5 * 1024 * 1024)
                {
                    lastProgressLog = DateTime.UtcNow;
                    lastLoggedBytes = bytes;
                    _ = jobRepo.UpdateJobAsync(job, CancellationToken.None);
                    _ = jobRepo.AppendLogAsync(JobExecutionLog.Create(
                        job.Id, correlationId, "StreamingToSink",
                        $"Streamed {bytes:N0} bytes ({records:N0} records) to {job.SinkType}",
                        "Trace", bytesSoFar: bytes), CancellationToken.None);
                }
            }

            // 4. Stream Source Chunks into Sink
            var sinkResult = await sink.WriteStreamAsync(job, sourceStream, OnProgress, ct);

            if (!sinkResult.IsSuccess)
            {
                throw new InvalidOperationException($"Sink delivery failed: {sinkResult.ErrorMessage}");
            }

            // 5. Finalize Job
            stopwatch.Stop();
            job.MarkCompleted(
                sinkResult.DestinationUri,
                sinkResult.ChecksumSha256,
                sinkResult.TotalBytesWritten,
                sinkResult.TotalRecordsWritten);

            await jobRepo.UpdateJobAsync(job, ct);
            await jobRepo.AppendLogAsync(JobExecutionLog.Create(
                job.Id, correlationId, "Completed",
                $"Successfully completed extraction in {stopwatch.Elapsed.TotalSeconds:F2}s. Total: {sinkResult.TotalBytesWritten:N0} bytes, {sinkResult.TotalRecordsWritten:N0} records. Destination: {sinkResult.DestinationUri}. Checksum (SHA-256): {sinkResult.ChecksumSha256}",
                "Info", bytesSoFar: sinkResult.TotalBytesWritten), ct);

            // Record OpenTelemetry Metrics
            var sourceTag = new KeyValuePair<string, object?>("source", job.SourceType.ToString());
            var sinkTag = new KeyValuePair<string, object?>("sink", job.SinkType.ToString());

            NexusObservability.JobsCompletedCounter.Add(1, sourceTag, sinkTag);
            NexusObservability.ActiveJobsCounter.Add(-1);
            NexusObservability.BytesStreamedCounter.Add(sinkResult.TotalBytesWritten, sourceTag, sinkTag);
            NexusObservability.RecordsStreamedCounter.Add(sinkResult.TotalRecordsWritten, sourceTag, sinkTag);
            NexusObservability.JobDurationHistogram.Record(stopwatch.Elapsed.TotalSeconds, sourceTag, sinkTag);

            _logger.LogInformation("Job {JobId} successfully finalized at {Dest} in {Elapsed:F2}s (SHA-256: {Hash})",
                job.Id, sinkResult.DestinationUri, stopwatch.Elapsed.TotalSeconds, sinkResult.ChecksumSha256);

            await messageBus.PublishAsync("job-events", new
            {
                jobId = job.Id,
                status = "Completed",
                destination = sinkResult.DestinationUri,
                checksum = sinkResult.ChecksumSha256,
                durationSeconds = stopwatch.Elapsed.TotalSeconds
            }, correlationId, ct);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Extraction pipeline error for Job {JobId}: {Msg}", job.Id, ex.Message);

            var sourceTag = new KeyValuePair<string, object?>("source", job.SourceType.ToString());
            var sinkTag = new KeyValuePair<string, object?>("sink", job.SinkType.ToString());

            if (job.CanRetry())
            {
                job.IncrementRetry();
                var backoffSeconds = (int)Math.Pow(2, job.RetryCount); // Exponential backoff: 2s, 4s, 8s
                _logger.LogWarning("Scheduling retry {Attempt}/{Max} for Job {JobId} in {Backoff}s",
                    job.RetryCount, job.MaxRetries, job.Id, backoffSeconds);

                await jobRepo.UpdateJobAsync(job, CancellationToken.None);
                await jobRepo.AppendLogAsync(JobExecutionLog.Create(
                    job.Id, correlationId, "RetryScheduled",
                    $"Transient failure encountered: {ex.Message}. Retry {job.RetryCount}/{job.MaxRetries} scheduled in {backoffSeconds}s.",
                    "Warn", ex.ToString(), job.BytesExtracted), CancellationToken.None);

                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds));
                    await messageBus.PublishAsync("extraction-jobs", new ExtractionJobQueuedMessage(job.Id, correlationId), correlationId);
                });
            }
            else
            {
                // Retries Exhausted -> Route to Dead-Letter Queue (DLQ)
                _logger.LogError("Max retries exceeded for Job {JobId}. Routing to Dead-Letter Queue (DLQ).", job.Id);
                job.MarkDeadLettered($"Failed after {job.MaxRetries} retries: {ex.Message}", ex.ToString());

                await jobRepo.UpdateJobAsync(job, CancellationToken.None);
                await jobRepo.AppendLogAsync(JobExecutionLog.Create(
                    job.Id, correlationId, "DeadLetterQueue",
                    $"Job routed to Dead-Letter Queue (DLQ). Root error: {ex.Message}",
                    "Error", ex.ToString(), job.BytesExtracted), CancellationToken.None);

                NexusObservability.JobsFailedCounter.Add(1, sourceTag, sinkTag);
                NexusObservability.DeadLetterMessagesCounter.Add(1, sourceTag, sinkTag);
                NexusObservability.ActiveJobsCounter.Add(-1);

                // Publish to DLQ Topic
                await messageBus.PublishAsync("extraction-jobs-dlq", new
                {
                    jobId = job.Id,
                    jobName = job.JobName,
                    sourceType = job.SourceType,
                    sinkType = job.SinkType,
                    error = ex.Message,
                    retriesAttempted = job.RetryCount,
                    failedAtUtc = DateTime.UtcNow
                }, correlationId, CancellationToken.None);
            }
        }
    }
}
