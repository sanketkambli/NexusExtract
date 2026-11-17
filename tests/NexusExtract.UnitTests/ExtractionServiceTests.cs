using FluentAssertions;
using Moq;
using NexusExtract.Application.Contracts.Requests;
using NexusExtract.Application.Features.Extractions;
using NexusExtract.Application.Interfaces;
using NexusExtract.Domain.Entities;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;
using Xunit;

namespace NexusExtract.UnitTests;

public sealed class ExtractionServiceTests
{
    private readonly Mock<IJobRepository> _jobRepoMock = new();
    private readonly Mock<IMessageBus> _messageBusMock = new();
    private readonly Mock<IDiagnosticService> _diagnosticServiceMock = new();
    private readonly ExtractionService _sut;

    public ExtractionServiceTests()
    {
        _sut = new ExtractionService(_jobRepoMock.Object, _messageBusMock.Object, _diagnosticServiceMock.Object);
    }

    [Fact]
    public async Task TriggerExtractionAsync_WithValidRequest_AddsJobAndPublishesToMessageBus()
    {
        // Arrange
        var request = new TriggerExtractionRequest
        {
            JobName = "Daily Geneve Extract",
            SourceType = SourceType.GenevaSocket,
            SinkType = SinkType.LocalDisk,
            SourceConfigJson = "{}",
            SinkConfigJson = "{}"
        };

        // Act
        var result = await _sut.TriggerExtractionAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.JobName.Should().Be("Daily Geneve Extract");
        result.Value.Status.Should().Be(JobStatus.Queued);

        _jobRepoMock.Verify(r => r.AddJobAsync(It.IsAny<ExtractionJob>(), It.IsAny<CancellationToken>()), Times.Once);
        _messageBusMock.Verify(b => b.PublishAsync("extraction-jobs", It.IsAny<ExtractionJobQueuedMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriggerExtractionAsync_WithEmptyJobName_ReturnsFailure()
    {
        // Arrange
        var request = new TriggerExtractionRequest
        {
            JobName = "",
            SourceType = SourceType.GoTradeSybase,
            SinkType = SinkType.LocalDisk,
            SourceConfigJson = "{}",
            SinkConfigJson = "{}"
        };

        // Act
        var result = await _sut.TriggerExtractionAsync(request);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("JobName cannot be empty");
    }

    [Fact]
    public async Task CancelJobAsync_WhenJobIsRunning_CancelsSuccessfully()
    {
        // Arrange
        var job = ExtractionJob.Create("RunningJob", "CORR-1", SourceType.GoTradeSybase, SinkType.LocalDisk, "{}", "{}");
        job.MarkStarted();

        _jobRepoMock.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        // Act
        var result = await _sut.CancelJobAsync(job.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(JobStatus.Cancelled);
        _jobRepoMock.Verify(r => r.UpdateJobAsync(job, It.IsAny<CancellationToken>()), Times.Once);
    }
}
