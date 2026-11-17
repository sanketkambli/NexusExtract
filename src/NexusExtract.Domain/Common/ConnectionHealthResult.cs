namespace NexusExtract.Domain.Common;

public sealed record ConnectionHealthResult(
    bool IsSuccess,
    string Message,
    TimeSpan Latency,
    Dictionary<string, string>? Metadata = null
);
