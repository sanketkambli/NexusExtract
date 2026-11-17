using NexusExtract.Application.Common;
using NexusExtract.Application.Contracts.DTOs;
using NexusExtract.Application.Interfaces;
using NexusExtract.Domain.Common;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;

namespace NexusExtract.Application.Features.Sources;

public sealed class SourceManagementService
{
    private readonly IJobRepository _jobRepository;
    private readonly IDataSourceAdapterFactory _adapterFactory;

    public SourceManagementService(
        IJobRepository jobRepository,
        IDataSourceAdapterFactory adapterFactory)
    {
        _jobRepository = jobRepository;
        _adapterFactory = adapterFactory;
    }

    public async Task<IReadOnlyList<DataSourceDto>> ListSourcesAsync(CancellationToken ct = default)
    {
        var sources = await _jobRepository.GetAllSourcesAsync(ct);
        return sources.Select(s => new DataSourceDto(
            s.Id,
            s.Name,
            s.SourceType,
            s.Description,
            s.ConnectionConfigJson,
            s.IsActive,
            s.LastHealthCheckUtc,
            s.HealthStatus,
            s.LastHealthMessage,
            s.LastResponseTime
        )).ToList();
    }

    public async Task<Result<ConnectionHealthResult>> TestSourceConnectionAsync(string sourceId, CancellationToken ct = default)
    {
        var source = await _jobRepository.GetSourceByIdAsync(sourceId, ct);
        if (source is null)
        {
            return Result<ConnectionHealthResult>.Failure($"Source '{sourceId}' not found.");
        }

        var adapter = _adapterFactory.GetAdapter(source.SourceType);
        var result = await adapter.TestConnectionAsync(source.ConnectionConfigJson, ct);

        source.LastHealthCheckUtc = DateTime.UtcNow;
        source.HealthStatus = result.IsSuccess ? "Healthy" : "Degraded";
        source.LastHealthMessage = result.Message;
        source.LastResponseTime = result.Latency;

        await _jobRepository.SaveSourceAsync(source, ct);

        return Result<ConnectionHealthResult>.Success(result);
    }
}
