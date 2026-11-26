using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class EnvironmentalPhysiologyTests
{
    [Fact]
    public void Vapor_pressure_deficit_responds_to_humidity()
    {
        EnvironmentalPhysiology.VaporPressureDeficitHpa(25, 100).Should().Be(0);
        EnvironmentalPhysiology.VaporPressureDeficitHpa(25, 50)!.Value.Should().BeApproximately(15.8, 0.1);
        EnvironmentalPhysiology.VaporPressureDeficitHpa(25, 101).Should().BeNull();
    }

    [Fact]
    public void Grade_preserves_direction()
    {
        EnvironmentalPhysiology.GradePercent(10, 100).Should().Be(10);
        EnvironmentalPhysiology.GradePercent(-10, 100).Should().Be(-10);
        EnvironmentalPhysiology.GradePercent(10, 0).Should().BeNull();
    }

    [Fact]
    public void Exposure_uses_observed_duration_only()
    {
        var result = EnvironmentalPhysiology.SummarizeHeatExposure([20, 30, 35], [10, 20, 5], 25);
        result.Should().NotBeNull();
        result!.ObservedMinutes.Should().Be(35);
        result.AboveThresholdMinutes.Should().Be(25);
        result.DegreeMinutes.Should().Be(150);
    }

    [Fact]
    public void Exposure_at_threshold_has_no_excess_dose()
    {
        var result = EnvironmentalPhysiology.SummarizeHeatExposure([25, 20], [10, 10], 25);
        result.Should().NotBeNull();
        result!.AboveThresholdMinutes.Should().Be(0);
        result.DegreeMinutes.Should().Be(0);
    }

    [Fact]
    public void Missing_or_unpaired_exposure_is_not_imputed()
    {
        EnvironmentalPhysiology.SummarizeHeatExposure([25, 30], [10], 25).Should().BeNull();
        EnvironmentalPhysiology.SummarizeHeatExposure([double.NaN], [10], 25).Should().BeNull();
        EnvironmentalPhysiology.SummarizeHeatExposure([25], [0], 25).Should().BeNull();
    }
}
