using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class DurabilityProfileEngineTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Session = Guid.NewGuid();

    [Fact]
    public void Reports_sustained_onset_and_robust_rate_in_percentage_points_per_hour()
    {
        DurabilityWindow[] windows = [Window(0, 4), Window(1, 3.8), Window(2, 3.6), Window(3, 3.4)];
        var result = DurabilityProfileEngine.Analyze(windows, 0.8, 4, 2);
        result.Should().NotBeNull();
        result!.Onset.Should().Be(Start.AddHours(1));
        result.OnsetConfirmedAt.Should().Be(Start.AddHours(3));
        result.LossPercent[3].Should().BeApproximately(15, 0.000001);
        result.LossPercentagePointsPerHour.Should().BeApproximately(5, 0.000001);
        result.MedianAbsoluteResidualPercentagePoints.Should().BeApproximately(0, 0.000001);
    }

    [Fact]
    public void Isolated_crossing_is_not_a_sustained_onset()
    {
        DurabilityWindow[] windows = [Window(0, 4), Window(1, 3.6), Window(2, 4), Window(3, 3.6)];
        var result = DurabilityProfileEngine.Analyze(windows, 0.8, 5, 2);
        result.Should().NotBeNull();
        result!.Onset.Should().BeNull();
        result.OnsetConfirmedAt.Should().BeNull();
    }

    [Fact]
    public void Refuses_gaps_low_coverage_and_incompatible_metrics()
    {
        DurabilityProfileEngine.Analyze([Window(0, 4), Window(2, 3.8), Window(3, 3.6)], 0.8, 5, 1)
            .Should().BeNull();
        DurabilityProfileEngine.Analyze([Window(0, 4), Window(1, 3.8) with { PairedDuration = TimeSpan.Zero },
            Window(2, 3.6)], 0.8, 5, 1).Should().BeNull();
        DurabilityProfileEngine.Analyze([Window(0, 4), Window(1, 3.8),
            Window(2, 3.6) with { ExternalLoadMetric = ExternalLoadMetric.Power }], 0.8, 5, 1).Should().BeNull();
    }

    private static DurabilityWindow Window(int hour, double speed) =>
        new(Start.AddHours(hour), Start.AddHours(hour + 1), TimeSpan.FromHours(1), speed, 150, Session, ExternalLoadMetric.Speed);
}
