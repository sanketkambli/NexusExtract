using NexusExtract.Domain.Common;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;

namespace NexusExtract.Domain.Interfaces;

public interface IDataSourceAdapter
{
    SourceType SupportedType { get; }

    Task<ConnectionHealthResult> TestConnectionAsync(string connectionConfigJson, CancellationToken ct = default);

    IAsyncEnumerable<DataChunk> ExtractStreamAsync(ExtractionJob job, CancellationToken ct = default);
}
