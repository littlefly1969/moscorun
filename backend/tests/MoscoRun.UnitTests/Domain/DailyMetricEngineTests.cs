using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class DailyMetricEngineTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 11, 25);
    private static readonly DateTimeOffset Cutoff = At(Day.AddDays(1));

    [Fact]
    public void Weight_uses_distinct_days_and_robust_slope_not_a_fat_loss_estimate()
    {
        var rows = Enumerable.Range(0, 8).Select(i => Row(Day.AddDays(i - 7), 80 - i * 0.1)).ToArray();
        var report = DailyMetricEngine.Build(Owner, "weight", "scale", Day, 7, Cutoff, rows);
        report.Should().NotBeNull();
        report!.Status.Should().Be("observed");
        report.Baseline!.BaselineCount.Should().Be(7);
        report.Baseline.BaselineMedian.Should().BeApproximately(79.7, 1e-9);
        report.WeightTrendKgPerWeek.Should().BeApproximately(-0.7, 1e-9);
        report.Current.ObservationId.Should().Be(rows[^1].Observation.Id);
        report.CurrentVersusMedianPercent.Should().BeApproximately(100 * (79.3 / 79.7 - 1), 1e-9);
    }

    [Fact]
    public void Future_data_other_source_and_changed_method_do_not_enter_baseline()
    {
        var rows = Enumerable.Range(0, 8).Select(i => Row(Day.AddDays(i - 7), 80)).ToList();
        rows[0] = Row(Day.AddDays(-7), 500, available: Cutoff.AddSeconds(1));
        rows[1] = Row(Day.AddDays(-6), 500, source: "other");
        rows[2] = Row(Day.AddDays(-5), 500, method: ObservationMethod.ProviderReported);
        var report = DailyMetricEngine.Build(Owner, "weight", "scale", Day, 7, Cutoff, rows);
        report!.Status.Should().Be("insufficient-baseline");
        report.Baseline.Should().BeNull();
        report.History.Select(x => x.Status).Should().Equal("missing", "missing", "incompatible-method",
            "observed", "observed", "observed", "observed");
    }

    [Fact]
    public void Ambiguous_observations_and_wrong_owner_are_not_used()
    {
        var report = DailyMetricEngine.Build(Owner, "weight", "scale", Day, 7, Cutoff,
            [Row(Day, 80), Row(Day, 81), Row(Day.AddDays(-1), 79)]);
        report!.Status.Should().Be("ambiguous");
        report.Current.Value.Should().BeNull();
        report.History[^1].Value.Should().Be(79);
        var flagged = DailyMetricEngine.Build(Owner, "weight", "scale", Day, 7, Cutoff,
            [Row(Day, 80) with { RevisionAmbiguous = true }]);
        flagged!.Status.Should().Be("ambiguous");
        DailyMetricEngine.Build(Guid.NewGuid(), "weight", "scale", Day, 7, Cutoff, [Row(Day, 80)])
            .Should().BeNull();
    }

    [Fact]
    public void Zero_score_is_observed_but_constant_baseline_has_no_robust_z()
    {
        var rows = Enumerable.Range(0, 8).Select(i =>
        {
            var t = At(Day.AddDays(i - 7));
            return new DailyMetricCandidate(new ScientificObservation(Guid.NewGuid(), Owner, "fatigue",
                "0-10", 0, t, t, "athlete-feedback", ObservationMethod.SelfReported));
        });
        var report = DailyMetricEngine.Build(Owner, "fatigue", "athlete-feedback", Day, 7, Cutoff, rows);
        report!.Status.Should().Be("observed");
        report.Current.Value.Should().Be(0);
        report.Baseline!.RobustZScore.Should().BeNull();
        report.WeightTrendKgPerWeek.Should().BeNull();
        DailyMetricEngine.Build(Owner, "fatigue", "athlete-feedback", Day.AddDays(1), 7, Cutoff, rows)
            .Should().BeNull();
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static DailyMetricCandidate Row(DateOnly day, double value, string source = "scale",
        DateTimeOffset? available = null, ObservationMethod method = ObservationMethod.Measured)
    {
        var t = At(day);
        return new DailyMetricCandidate(new ScientificObservation(Guid.NewGuid(), Owner, "weight", "kg",
            value, t, available ?? t, source, method));
    }
}
