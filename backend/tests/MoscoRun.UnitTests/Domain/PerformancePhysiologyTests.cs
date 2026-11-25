using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class PerformancePhysiologyTests
{
    [Fact]
    public void Critical_speed_recovers_synthetic_parameters()
    {
        PerformanceEffort[] efforts = [new(180, 920, true), new(360, 1640, true), new(720, 3080, true)];
        var fit = PerformancePhysiology.FitCriticalSpeed(efforts);
        fit.Should().NotBeNull();
        fit!.CriticalSpeedMetersPerSecond.Should().BeApproximately(4, 0.000001);
        fit.DistanceReserveMeters.Should().BeApproximately(200, 0.000001);
        fit.RootMeanSquaredErrorMeters.Should().BeApproximately(0, 0.000001);
        fit.EffortCount.Should().Be(3);
    }

    [Fact]
    public void Non_maximal_efforts_do_not_identify_critical_speed()
    {
        PerformancePhysiology.FitCriticalSpeed([new(180, 920, true), new(360, 1640, true), new(720, 3080, false)])
            .Should().BeNull();
    }

    [Fact]
    public void Duplicate_durations_do_not_count_as_independent_durations()
    {
        PerformancePhysiology.FitCriticalSpeed([new(180, 920, true), new(180, 910, true), new(360, 1640, true)])
            .Should().BeNull();
    }

    [Fact]
    public void Nonphysical_distance_reserve_is_rejected()
    {
        PerformancePhysiology.FitCriticalSpeed([new(180, 620, true), new(360, 1340, true), new(720, 2780, true)])
            .Should().BeNull();
    }

    [Fact]
    public void Efficiency_is_meters_per_beat()
    {
        PerformancePhysiology.CardiovascularEfficiency(4, 160).Should().Be(1.5);
        PerformancePhysiology.CardiovascularEfficiency(4, 0).Should().BeNull();
    }

    [Fact]
    public void Observed_speed_does_not_extrapolate_duration()
    {
        PerformanceEffort[] efforts = [new(180, 900, false), new(180, 810, true), new(360, 1800, true)];
        PerformancePhysiology.ObservedBestSpeed(efforts, 180).Should().Be(5);
        PerformancePhysiology.ObservedBestSpeed(efforts, 90).Should().BeNull();
    }
}
