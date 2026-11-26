using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class BiomechanicsEngineTests
{
    [Fact]
    public void Step_length_uses_total_steps_per_minute()
    {
        BiomechanicsEngine.StepLengthMeters(3, 180).Should().Be(1);
        BiomechanicsEngine.StepLengthMeters(3, 0).Should().BeNull();
    }

    [Fact]
    public void Duty_factor_uses_same_foot_cycle()
    {
        BiomechanicsEngine.DutyFactorPercent(250, 180).Should().Be(37.5);
        BiomechanicsEngine.DutyFactorPercent(900, 180).Should().BeNull();
    }

    [Fact]
    public void Vertical_ratio_uses_same_length_unit()
    {
        BiomechanicsEngine.VerticalRatioPercent(0.08, 1).Should().Be(8);
        BiomechanicsEngine.VerticalRatioPercent(0.08, 0).Should().BeNull();
    }

    [Fact]
    public void Asymmetry_keeps_direction()
    {
        BiomechanicsEngine.AsymmetryPercent(110, 90).Should().Be(20);
        BiomechanicsEngine.AsymmetryPercent(90, 110).Should().Be(-20);
        BiomechanicsEngine.AsymmetryPercent(0, 110).Should().BeNull();
    }

    [Fact]
    public void Cadence_variability_requires_multiple_valid_values()
    {
        BiomechanicsEngine.CoefficientOfVariationPercent([180, 180, double.NaN]).Should().Be(0);
        BiomechanicsEngine.CoefficientOfVariationPercent([180, double.NaN]).Should().BeNull();
    }
}
