using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class RacePredictionPacingTests
{
    [Fact]
    public void Riegel_preserves_reference_and_explicit_exponent()
    {
        RacePredictionEngine.Riegel(5000, 1200, 5000, 1.06)!.PredictedSeconds.Should().Be(1200);
        RacePredictionEngine.Riegel(5000, 1200, 10000, 1.06)!.PredictedSeconds
            .Should().BeApproximately(1200 * Math.Pow(2, 1.06), 0.000001);
        RacePredictionEngine.Riegel(5000, 1200, 10000, 0).Should().BeNull();
    }

    [Fact]
    public void Critical_speed_prediction_does_not_extrapolate_to_marathon()
    {
        var fit = new CriticalSpeedFit(4, 200, 0, 3);
        RacePredictionEngine.CriticalSpeed(fit, 2600)!.PredictedSeconds.Should().Be(600);
        RacePredictionEngine.CriticalSpeed(fit, 42195).Should().BeNull();
        RacePredictionEngine.CriticalSpeed(fit, 100).Should().BeNull();
    }

    [Fact]
    public void Calibration_requires_enough_residuals_for_requested_rank()
    {
        double[] errors = [1, 2, 3, 4, 5, 6, 7, 8, 9];
        var result = RacePredictionEngine.Calibrate(100, errors, 0.8);
        result.Should().NotBeNull();
        result!.LowerSeconds.Should().Be(92);
        result.UpperSeconds.Should().Be(108);
        RacePredictionEngine.Calibrate(100, errors, 0.99).Should().BeNull();
    }

    [Fact]
    public void Pacing_allocates_total_time_without_assuming_grade_cost()
    {
        var splits = PacingEngine.Allocate([new(1000, 1), new(1000, 2)], 900);
        splits.Should().NotBeNull();
        splits![0].SegmentSeconds.Should().Be(300);
        splits[1].SegmentSeconds.Should().Be(600);
        splits[1].CumulativeSeconds.Should().Be(900);
        PacingEngine.Allocate([new(1000, 0)], 900).Should().BeNull();
    }
}
