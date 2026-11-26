using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class AdvancedSignalPerformanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Causal_grid_does_not_fill_from_future_or_cross_missing_sample()
    {
        SignalSample[] samples = [new(Now.AddSeconds(1), 4), new(Now.AddSeconds(3), null),
            new(Now.AddSeconds(5), 8)];
        var grid = CausalSignalGrid.Resample(samples, Now, Now.AddSeconds(6), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        grid.Should().NotBeNull();
        grid!.Select(item => item.Value).Should().Equal(null, 4, 4, null, null, 8);
        CausalSignalGrid.BestObservedMean(grid, TimeSpan.FromSeconds(1), 2).Should().Be(4);
        CausalSignalGrid.BestObservedMean(grid, TimeSpan.FromSeconds(1), 3).Should().BeNull();
    }

    [Fact]
    public void Robust_fit_resists_single_outlier()
    {
        var fit = RobustLineRegression.Fit([1, 2, 3, 4, 5], [3, 5, 90, 9, 11]);
        fit.Should().NotBeNull();
        fit!.Slope.Should().Be(2);
        fit.Intercept.Should().Be(1);
    }

    [Fact]
    public void Power_duration_fit_recovers_work_reserve()
    {
        var fit = AdvancedPerformancePhysiology.FitCriticalPower([new(180, 300, true),
            new(360, 250, true), new(720, 225, true)]);
        fit.Should().NotBeNull();
        fit!.CriticalWatts.Should().BeApproximately(200, 0.000001);
        fit.WorkReserveJoules.Should().BeApproximately(18000, 0.000001);
    }

    [Fact]
    public void Reserve_depletes_recovers_and_stops_at_exhaustion()
    {
        var states = CriticalReserveEngine.Replay([new(20, 5, 30), new(30, 3, 30), new(200, 5, 30)], 4, 100, 100);
        states.Should().NotBeNull();
        states![0].Remaining.Should().Be(80);
        states[1].Remaining.Should().BeApproximately(100 - 20 * Math.Exp(-1), 0.000001);
        states[2].Exhausted.Should().BeTrue();
        states[2].Remaining.Should().Be(0);
    }

    [Fact]
    public void Field_vo2_estimate_requires_attested_protocol()
    {
        AdvancedPerformancePhysiology.CooperVo2MaxEstimate(3000, 720, protocolAttested: true)!.Value
            .Should().BeApproximately(55.765, 0.000001);
        AdvancedPerformancePhysiology.CooperVo2MaxEstimate(3000, 700, protocolAttested: true).Should().BeNull();
        AdvancedPerformancePhysiology.CooperVo2MaxEstimate(3000, 720, protocolAttested: false).Should().BeNull();
    }

    [Fact]
    public void Threshold_interpolates_only_measured_stages()
    {
        LactateStage[] stages = [new(3, 2, 130), new(4, 3, 150), new(5, 5, 170)];
        var estimate = LactateThresholdEngine.Interpolate(stages, 4, protocolAttested: true);
        estimate.Should().NotBeNull();
        estimate!.SpeedMetersPerSecond.Should().Be(4.5);
        estimate.HeartRateBpm.Should().Be(160);
        LactateThresholdEngine.Interpolate(stages, 6, protocolAttested: true).Should().BeNull();
    }
}
