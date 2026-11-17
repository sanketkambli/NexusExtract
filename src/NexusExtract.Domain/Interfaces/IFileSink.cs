using NexusExtract.Domain.Common;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;

namespace NexusExtract.Domain.Interfaces;

public interface IFileSink
{
    SinkType SupportedType { get; }

    Task<SinkWriteResult> WriteStreamAsync(
        ExtractionJob job,
        IAsyncEnumerable<DataChunk> stream,
        Action<long, long> onProgress,
        CancellationToken ct = default);
}
