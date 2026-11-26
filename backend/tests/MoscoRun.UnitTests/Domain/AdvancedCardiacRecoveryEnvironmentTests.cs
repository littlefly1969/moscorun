using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class AdvancedCardiacRecoveryEnvironmentTests
{
    [Fact]
    public void Response_calibration_recovers_synthetic_time_constant()
    {
        var points = new[] { 10.0, 20, 40, 80 }.Select(time =>
            new CardiacResponsePoint(time, 160 - 60 * Math.Exp(-time / 30))).ToArray();
        var fit = CardiacResponseCalibration.Fit(points, 100, 160, [10, 20, 30, 40]);
        fit.Should().NotBeNull();
        fit!.TimeConstantSeconds.Should().Be(30);
        fit.RootMeanSquaredErrorBpm.Should().BeApproximately(0, 0.000001);
    }

    [Fact]
    public void Dfa_rejects_unscreened_and_constant_intervals()
    {
        var values = Enumerable.Repeat(800.0, 128).ToArray();
        DfaAlpha1Engine.Estimate(values, artifactScreened: true).Should().BeNull();
        DfaAlpha1Engine.Estimate(values, artifactScreened: false).Should().BeNull();
    }

    [Fact]
    public void Dfa_computes_scale_exponent_without_interpreting_threshold()
    {
        var values = Enumerable.Range(0, 256).Select(index => 800 + 20 * Math.Sin(index * 0.27) +
            5 * Math.Cos(index * 0.81)).ToArray();
        var result = DfaAlpha1Engine.Estimate(values, artifactScreened: true);
        result.Should().NotBeNull();
        result!.ScaleCount.Should().Be(13);
        result.IntervalCount.Should().Be(256);
        double.IsFinite(result.Alpha1).Should().BeTrue();
    }

    [Fact]
    public void Sleep_timing_wraps_around_midnight()
    {
        var timing = SleepTimingEngine.Summarize([1430, 10, 1430, 10, 1430, 10, 0]);
        timing.Should().NotBeNull();
        Math.Min(timing!.MeanMinuteOfDay, 1440 - timing.MeanMinuteOfDay).Should().BeApproximately(0, 0.000001);
        timing.CircularStandardDeviationMinutes.Should().BeLessThan(15);
    }

    [Fact]
    public void Wind_direction_and_gps_distance_have_explicit_conventions()
    {
        EnvironmentalContext.HeadwindMetersPerSecond(5, 90, 90).Should().Be(5);
        EnvironmentalContext.HeadwindMetersPerSecond(5, 270, 90).Should().Be(-5);
        EnvironmentalContext.HorizontalDistanceMeters(0, 179.9, 0, -179.9)!.Value.Should().BeApproximately(22239, 2);
        EnvironmentalContext.HorizontalDistanceMeters(91, 0, 0, 0).Should().BeNull();
    }
}
