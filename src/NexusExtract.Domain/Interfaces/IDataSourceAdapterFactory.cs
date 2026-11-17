using NexusExtract.Domain.Enums;

namespace NexusExtract.Domain.Interfaces;

public interface IDataSourceAdapterFactory
{
    IDataSourceAdapter GetAdapter(SourceType sourceType);
}

public interface IFileSinkFactory
{
    IFileSink GetSink(SinkType sinkType);
}
