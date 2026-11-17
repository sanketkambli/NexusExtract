using NexusExtract.Application.Interfaces;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Infrastructure.Sinks;

public sealed class FileSinkFactory : IFileSinkFactory
{
    private readonly Dictionary<SinkType, IFileSink> _sinks;

    public FileSinkFactory(IEnumerable<IFileSink> sinks)
    {
        _sinks = sinks.ToDictionary(s => s.SupportedType, s => s);
    }

    public IFileSink GetSink(SinkType sinkType)
    {
        // NetworkShare maps directly to LocalDiskSink with SMB/UNC path semantics
        var targetType = sinkType == SinkType.NetworkShare ? SinkType.LocalDisk : sinkType;

        if (_sinks.TryGetValue(targetType, out var sink))
        {
            return sink;
        }

        throw new NotSupportedException($"Sink type '{sinkType}' is not supported.");
    }
}
