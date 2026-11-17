namespace NexusExtract.Domain.Entities;

public sealed class JobExecutionLog
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string LogLevel { get; set; } = "Info"; // Info, Warning, Error, Trace
    public string Stage { get; set; } = "General"; // Auth, Handshake, StreamChunk, FlushSink, Checksum
    public string Message { get; set; } = string.Empty;
    public string? ExceptionDetails { get; set; }
    public long BytesTransferredSoFar { get; set; }
    public Dictionary<string, string>? ContextData { get; set; }

    public static JobExecutionLog Create(
        Guid jobId,
        string correlationId,
        string stage,
        string message,
        string logLevel = "Info",
        string? exception = null,
        long bytesSoFar = 0)
    {
        return new JobExecutionLog
        {
            JobId = jobId,
            CorrelationId = correlationId,
            TimestampUtc = DateTime.UtcNow,
            Stage = stage,
            Message = message,
            LogLevel = logLevel,
            ExceptionDetails = exception,
            BytesTransferredSoFar = bytesSoFar
        };
    }
}
