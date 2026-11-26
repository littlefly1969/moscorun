using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class WearableAcquisitionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    [Theory]
    [InlineData("fit:verticalOscillation", "mm", 80, "verticalOscillation", "m", 0.08)]
    [InlineData("fit:stepLength", "mm", 1200, "stepLength", "m", 1.2)]
    [InlineData("fit:stanceTime", "ms", 230, "groundContactTime", "ms", 230)]
    [InlineData("fit:verticalRatio", "%", 8, "verticalRatio", "%", 8)]
    [InlineData("fit:respirationRate", "1/min", 30, "respirationRate", "1/min", 30)]
    public void Garmin_mapping_converts_only_explicit_units(
        string nativeMetric, string nativeUnit, double value, string metric, string unit, double expected)
    {
        var observation = Map(nativeMetric, nativeUnit, value);
        observation.Metric.Should().Be(metric);
        observation.Unit.Should().Be(unit);
        observation.Value.Should().BeApproximately(expected, 0.000001);
        observation.Method.Should().Be(ObservationMethod.ProviderReported);
    }

    [Fact]
    public void Ambiguous_cadence_remains_native()
    {
        var observation = Map("fit:cadence", "rpm", 85);
        observation.IsMapped.Should().BeFalse();
        observation.Value.Should().Be(85);
    }

    [Fact]
    public void Missing_value_remains_missing()
    {
        var observation = Map("fit:stepLength", "mm", null);
        observation.Value.Should().BeNull();
        observation.Unit.Should().Be("m");
    }

    [Fact]
    public void Capability_distinguishes_missing_from_not_observed()
    {
        var observation = Map("fit:stepLength", "mm", null);
        WearableCapabilityAudit.GetAvailability(Owner, Now, "garmin-connect", "stepLength", [observation])
            .Should().Be(WearableChannelAvailability.ReportedMissing);
        WearableCapabilityAudit.GetAvailability(Owner, Now, "garmin-connect", "rrInterval", [observation])
            .Should().Be(WearableChannelAvailability.NotObserved);
    }

    [Fact]
    public void Capability_excludes_future_observations()
    {
        var observation = WearableMetricMapping.Normalize(Guid.NewGuid(), Owner, "heartRate", "bpm", 150,
            Now, Now.AddMinutes(1), "garmin-connect");
        WearableCapabilityAudit.GetAvailability(Owner, Now, "garmin-connect", "heartRate", [observation])
            .Should().Be(WearableChannelAvailability.NotObserved);
        WearableCapabilityAudit.GetAvailability(Owner, Now.AddMinutes(1), "garmin-connect", "heartRate", [observation])
            .Should().Be(WearableChannelAvailability.Observed);
    }

    private static ScientificObservation Map(string metric, string unit, double? value) =>
        WearableMetricMapping.Normalize(Guid.NewGuid(), Owner, metric, unit, value, Now, Now, "garmin-connect");
}
