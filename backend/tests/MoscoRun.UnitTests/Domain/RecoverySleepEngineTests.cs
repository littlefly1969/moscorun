using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class RecoverySleepEngineTests
{
    [Fact]
    public void Baseline_is_robust_to_extreme_value()
    {
        var result = RecoverySleepEngine.CompareWithBaseline(65, [50, 51, 52, 53, 54, 55, 1000]);
        result.Should().NotBeNull();
        result!.BaselineMedian.Should().Be(53);
        result.MedianAbsoluteDeviation.Should().Be(2);
        result.RobustZScore!.Value.Should().BeApproximately(4.046939, 0.000001);
    }

    [Fact]
    public void Baseline_requires_history_and_nonzero_variability()
    {
        RecoverySleepEngine.CompareWithBaseline(65, [50, 51]).Should().BeNull();
        var result = RecoverySleepEngine.CompareWithBaseline(65, [50, 50, 50, 50, 50, 50, 50]);
        result.Should().NotBeNull();
        result!.RobustZScore.Should().BeNull();
    }

    [Fact]
    public void Sleep_efficiency_rejects_impossible_duration()
    {
        RecoverySleepEngine.SleepEfficiencyPercent(420, 480).Should().Be(87.5);
        RecoverySleepEngine.SleepEfficiencyPercent(500, 480).Should().BeNull();
    }

    [Fact]
    public void Sleep_timing_wraps_around_midnight()
    {
        RecoverySleepEngine.CircularTimingDifferenceMinutes(1410, 30).Should().Be(60);
        RecoverySleepEngine.CircularTimingDifferenceMinutes(1440, 30).Should().BeNull();
    }

    [Fact]
    public void Correlation_is_descriptive_and_requires_paired_data()
    {
        double[] sleep = [5, 6, 7, 8, 9, 10, 11];
        RecoverySleepEngine.SleepPerformanceCorrelation(sleep, [10, 12, 14, 16, 18, 20, 22])!.Value
            .Should().BeApproximately(1, 0.000001);
        RecoverySleepEngine.SleepPerformanceCorrelation(sleep, [10, 10, 10, 10, 10, 10, 10]).Should().BeNull();
        RecoverySleepEngine.SleepPerformanceCorrelation(sleep, [10, 12]).Should().BeNull();
    }
}
