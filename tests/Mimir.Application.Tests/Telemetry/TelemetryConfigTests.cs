using System.Diagnostics;
using Mimir.Application.Telemetry;
using Shouldly;

namespace Mimir.Application.Tests.Telemetry;

public sealed class TelemetryConfigTests
{
    [Fact]
    public void NemActivitySource_Source_ShouldHaveCorrectName()
    {
        // Act
        var source = NemActivitySource.Source;

        // Assert
        source.ShouldNotBeNull();
        source.Name.ShouldBe("nem.Mimir");
        source.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public void NemActivitySource_StartActivity_ShouldReturnActivityWhenEnabled()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        var activity = NemActivitySource.StartActivity("test.operation");

        // Assert
        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("test.operation");
        activity.Kind.ShouldBe(ActivityKind.Internal);

        // Cleanup
        listener.Dispose();
    }

    [Fact]
    public void NemActivitySource_StartServerActivity_ShouldReturnServerActivity()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        var activity = NemActivitySource.StartServerActivity("test.request");

        // Assert
        activity.ShouldNotBeNull();
        activity.Kind.ShouldBe(ActivityKind.Server);

        // Cleanup
        listener.Dispose();
    }

    [Fact]
    public void NemActivitySource_StartInternalActivity_ShouldReturnInternalActivity()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        var activity = NemActivitySource.StartInternalActivity("test.process");

        // Assert
        activity.ShouldNotBeNull();
        activity.Kind.ShouldBe(ActivityKind.Internal);

        // Cleanup
        listener.Dispose();
    }

    [Fact]
    public void NemActivitySource_SourceName_CanBeUsedToFilter()
    {
        // Act & Assert
        NemActivitySource.Source.Name.ShouldBe("nem.Mimir");
        NemActivitySource.Source.Version.ShouldBe("1.0.0");
    }
}
