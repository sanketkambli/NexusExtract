namespace NexusExtract.Domain.Enums;

public enum JobStatus
{
    Pending,
    Queued,
    Connecting,
    Extracting,
    StreamingToSink,
    Completed,
    Failed,
    Cancelled,
    DeadLettered
}
