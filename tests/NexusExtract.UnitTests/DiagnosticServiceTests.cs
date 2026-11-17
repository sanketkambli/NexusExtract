using FluentAssertions;
using NexusExtract.Application.Services;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using Xunit;

namespace NexusExtract.UnitTests;

public sealed class DiagnosticServiceTests
{
    private readonly DiagnosticService _sut = new();

    [Fact]
    public void AnalyzeJob_WhenJobIsCompleted_ReturnsNoneRootCauseAndVerifiedRemediation()
    {
        // Arrange
        var job = ExtractionJob.Create("Job1", "CORR-1", SourceType.GoTradeSybase, SinkType.LocalDisk, "{}", "{}");
        job.MarkCompleted("e:/data/out.csv", "abc123sha", 1000, 50);

        // Act
        var result = _sut.AnalyzeJob(job, []);

        // Assert
        result.Status.Should().Be(JobStatus.Completed);
        result.RootCauseCategory.Should().Be("None");
        result.SuggestedRemediation.Should().Contain("verified with SHA-256");
    }

    [Fact]
    public void AnalyzeJob_WhenSybaseDeadlock1205_ClassifiesAsDatabaseConcurrency()
    {
        // Arrange
        var job = ExtractionJob.Create("SybaseJob", "CORR-2", SourceType.GoTradeSybase, SinkType.LocalDisk, "{}", "{}");
        job.MarkFailed("Sybase error 1205: Transaction was deadlocked on lock resources with another process and has been chosen as the deadlock victim.");

        var logs = new List<JobExecutionLog>
        {
            JobExecutionLog.Create(job.Id, job.CorrelationId, "Extracting", "Lock escalation failed. Error 1205.", "Error")
        };

        // Act
        var result = _sut.AnalyzeJob(job, logs);

        // Assert
        result.Status.Should().Be(JobStatus.Failed);
        result.RootCauseCategory.Should().Be("DatabaseConcurrency");
        result.SuggestedRemediation.Should().Contain("Safe to retry extraction job");
    }

    [Fact]
    public void AnalyzeJob_WhenGenevaConnectionRefused_ClassifiesAsSourceProtocolError()
    {
        // Arrange
        var job = ExtractionJob.Create("GenevaJob", "CORR-3", SourceType.GenevaSocket, SinkType.LocalDisk, "{}", "{}");
        job.MarkFailed("System.Net.Sockets.SocketException: Connection refused by 127.0.0.1:9100");

        // Act
        var result = _sut.AnalyzeJob(job, []);

        // Assert
        result.RootCauseCategory.Should().Be("SourceProtocolError");
        result.SuggestedRemediation.Should().Contain("Verify the Geneva TCP listener is running");
    }

    [Fact]
    public void AnalyzeJob_WhenTokenExpired401_ClassifiesAsAuthentication()
    {
        // Arrange
        var job = ExtractionJob.Create("RestJob", "CORR-4", SourceType.GenericRestApi, SinkType.CloudStorageS3Minio, "{}", "{}");
        job.MarkFailed("HTTP 401 Unauthorized: token expired or invalid client credentials");

        // Act
        var result = _sut.AnalyzeJob(job, []);

        // Assert
        result.RootCauseCategory.Should().Be("Authentication");
        result.SuggestedRemediation.Should().Contain("No developer action required");
    }
}
