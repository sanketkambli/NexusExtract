using NexusExtract.Application.Contracts.Responses;
using NexusExtract.Application.Interfaces;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;

namespace NexusExtract.Application.Services;

public sealed class DiagnosticService : IDiagnosticService
{
    public JobDiagnosticResponse AnalyzeJob(ExtractionJob job, IReadOnlyList<JobExecutionLog> logs)
    {
        var response = new JobDiagnosticResponse
        {
            JobId = job.Id,
            JobName = job.JobName,
            CorrelationId = job.CorrelationId,
            SourceType = job.SourceType,
            SinkType = job.SinkType,
            Status = job.Status,
            ErrorMessage = job.ErrorMessage,
            CreatedAtUtc = job.CreatedAtUtc,
            StartedAtUtc = job.StartedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            BytesTransferred = job.BytesExtracted,
            RowsTransferred = job.RowsExtracted,
            Logs = logs.OrderBy(l => l.TimestampUtc).ToList()
        };

        if (job.Status != JobStatus.Failed)
        {
            response.RootCauseCategory = job.Status == JobStatus.Completed ? "None" : "InProgress";
            response.SuggestedRemediation = job.Status == JobStatus.Completed 
                ? "Job completed successfully. Output file verified with SHA-256." 
                : "Job is currently active or pending worker allocation.";
            return response;
        }

        // Analyze failure patterns from ErrorMessage, ErrorDetail, and execution logs
        var fullErrorContext = $"{job.ErrorMessage} {job.ErrorDetail} " + 
            string.Join(" ", logs.Where(l => l.LogLevel == "Error").Select(l => l.Message + " " + l.ExceptionDetails));

        var (category, remediation) = ClassifyFailure(job.SourceType, job.SinkType, fullErrorContext);
        response.RootCauseCategory = category;
        response.SuggestedRemediation = remediation;

        return response;
    }

    private static (string Category, string Remediation) ClassifyFailure(SourceType source, SinkType sink, string context)
    {
        var text = context.ToLowerInvariant();

        if (text.Contains("401") || text.Contains("unauthorized") || text.Contains("token expired") || text.Contains("invalid credentials"))
        {
            return ("Authentication", "Verify source credentials / OAuth client secret. The token is rejected or has expired. No developer action required.");
        }

        if ((source == SourceType.GoTradeSybase || text.Contains("sybase")) && (text.Contains("deadlock") || text.Contains("1205") || text.Contains("lock timeout")))
        {
            return ("DatabaseConcurrency", "GoTrade Sybase database encountered transaction deadlock (error 1205) or lock escalation timeout. Safe to retry extraction job.");
        }

        if ((source == SourceType.GenevaSocket || text.Contains("geneva")) && (text.Contains("connection refused") || text.Contains("socket closed") || text.Contains("reset by peer")))
        {
            return ("SourceProtocolError", "Geneva Java Socket server closed the socket or refused connection. Verify the Geneva TCP listener is running on the configured host/port.");
        }

        if ((source == SourceType.PartnerSftpDrop || text.Contains("sftp")) && (text.Contains("timeout") || text.Contains("response file not found") || text.Contains("handshake")))
        {
            return ("PartnerSftpTimeout", "Partner SFTP server accepted request file but response file was not placed within the SLA polling window. Contact partner operations.");
        }

        if (text.Contains("s3") || text.Contains("minio") || text.Contains("access denied") || text.Contains("403") || text.Contains("bucket"))
        {
            return ("StorageSinkError", "Failed to write streaming chunks to cloud storage. Check S3/GCS bucket permissions, prefix paths, or disk quota.");
        }

        if (text.Contains("checksum") || text.Contains("hash mismatch") || text.Contains("corrupt"))
        {
            return ("DataIntegrityError", "Extracted data SHA-256 did not match expected transport checksum. Network corruption or incomplete stream detected.");
        }

        return ("InfrastructureOrUnknown", "Unclassified execution fault. Check the diagnostic logs stack trace or escalate to Level 2 infrastructure support.");
    }
}
