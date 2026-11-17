using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Adapters;

public sealed class DataSourceAdapterFactory : IDataSourceAdapterFactory
{
    private readonly Dictionary<SourceType, IDataSourceAdapter> _adapters;

    public DataSourceAdapterFactory(IEnumerable<IDataSourceAdapter> adapters)
    {
        _adapters = adapters.ToDictionary(a => a.SupportedType, a => a);
    }

    public IDataSourceAdapter GetAdapter(SourceType sourceType)
    {
        if (_adapters.TryGetValue(sourceType, out var adapter))
        {
            return adapter;
        }

        throw new NotSupportedException($"Data source type '{sourceType}' does not have a registered adapter.");
    }
}
