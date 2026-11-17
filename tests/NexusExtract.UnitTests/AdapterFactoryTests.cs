using FluentAssertions;
using Moq;
using NexusExtract.Adapters;
using NexusExtract.Domain.Enums;
using NexusExtract.Domain.Interfaces;
using Xunit;

namespace NexusExtract.UnitTests;

public sealed class AdapterFactoryTests
{
    [Fact]
    public void GetAdapter_WhenAdapterExists_ReturnsMatchingInstance()
    {
        // Arrange
        var mockAdapter = new Mock<IDataSourceAdapter>();
        mockAdapter.SetupGet(a => a.SupportedType).Returns(SourceType.GenevaSocket);

        var factory = new DataSourceAdapterFactory([mockAdapter.Object]);

        // Act
        var result = factory.GetAdapter(SourceType.GenevaSocket);

        // Assert
        result.Should().BeSameAs(mockAdapter.Object);
    }

    [Fact]
    public void GetAdapter_WhenAdapterNotRegistered_ThrowsNotSupportedException()
    {
        // Arrange
        var factory = new DataSourceAdapterFactory([]);

        // Act
        var act = () => factory.GetAdapter(SourceType.PartnerSftpDrop);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*PartnerSftpDrop*");
    }
}
