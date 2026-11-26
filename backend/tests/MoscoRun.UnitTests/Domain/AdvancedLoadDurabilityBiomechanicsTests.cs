using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class AdvancedLoadDurabilityBiomechanicsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Exponential_trimp_uses_explicit_coefficients()
    {
        AdvancedTrainingLoad.ExponentialTrimp(60, 120, 60, 180, 0.64, 1.92)!.Value
            .Should().BeApproximately(30 * 0.64 * Math.Exp(0.96), 0.000001);
        AdvancedTrainingLoad.ExponentialTrimp(60, 200, 60, 180, 0.64, 1.92).Should().BeNull();
    }

    [Fact]
    public void Intensity_distribution_distinguishes_missing_from_low_intensity()
    {
        var result = AdvancedTrainingLoad.Distribute([new(10, 2), new(20, 3), new(10, null)], [3, 5]);
        result.Should().NotBeNull();
        result!.ZoneMinutes.Should().Equal(10, 20, 0);
        result.Coverage.Should().Be(0.75);
        result.MissingMinutes.Should().Be(10);
    }

    [Fact]
    public void Rolling_comparison_uses_disjoint_windows_and_explicit_rest()
    {
        var last = new DateOnly(2026, 11, 25);
        DailyTrainingLoad[] loads = [new(last.AddDays(-3), Now, 0), new(last.AddDays(-2), Now, 20),
            new(last.AddDays(-1), Now, 20), new(last, Now, 20)];
        var result = RollingTrainingLoad.Compare(loads, last, Now, 2, 2);
        result.Should().NotBeNull();
        result!.PriorMean.Should().Be(10);
        result.RecentMean.Should().Be(20);
        result.RecentToPriorRatio.Should().Be(2);
        RollingTrainingLoad.Compare(loads.Skip(1).ToArray(), last, Now, 2, 2).Should().BeNull();
    }

    [Fact]
    public void Repeatability_reports_typical_error_not_independent_sample_count()
    {
        DurabilityRepeat[] pairs = [Pair(10, 12), Pair(10, 10), Pair(10, 8)];
        var result = DurabilityRepeatability.Assess(pairs);
        result.Should().NotBeNull();
        result!.Bias.Should().Be(0);
        result.TypicalError.Should().BeApproximately(Math.Sqrt(2), 0.000001);
        DurabilityRepeatability.Assess([pairs[0], pairs[0], pairs[2]]).Should().BeNull();
    }

    [Fact]
    public void Biomechanical_matching_excludes_other_devices_and_late_history()
    {
        var current = Session(0, 250);
        BiomechanicalSession[] history = [Session(-1, 230), Session(-2, 240), Session(-3, 250),
            Session(-4, 900) with { Source = "other" }, Session(-5, 999) with { AvailableAt = Now.AddHours(1) }];
        var result = BiomechanicalContextEngine.Compare(current, history, Now, 0.1, 0.5);
        result.Should().NotBeNull();
        result!.MatchedSessions.Should().Be(3);
        result.PriorMedian.Should().Be(240);
        result.Difference.Should().Be(10);
    }

    private static DurabilityRepeat Pair(double test, double retest) =>
        new(Owner, Guid.NewGuid(), Guid.NewGuid(), "protocol-1", "device-1", ExternalLoadMetric.Speed, test, retest);

    private static BiomechanicalSession Session(int day, double value)
    {
        var time = Now.AddDays(day);
        return new BiomechanicalSession(Owner, Guid.NewGuid(), time, time, "device-1",
            "groundContactTime", "ms", 4, 0, value);
    }
}
