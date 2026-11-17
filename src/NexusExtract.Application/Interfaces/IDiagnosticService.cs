using NexusExtract.Application.Contracts.Responses;
using NexusExtract.Domain.Entities;

namespace NexusExtract.Application.Interfaces;

public interface IDiagnosticService
{
    JobDiagnosticResponse AnalyzeJob(ExtractionJob job, IReadOnlyList<JobExecutionLog> logs);
}
