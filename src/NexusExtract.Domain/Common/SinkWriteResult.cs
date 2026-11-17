namespace NexusExtract.Domain.Common;

public sealed record SinkWriteResult(
    bool IsSuccess,
    string DestinationUri,
    long TotalBytesWritten,
    long TotalRecordsWritten,
    string ChecksumSha256,
    string? ErrorMessage = null
);
