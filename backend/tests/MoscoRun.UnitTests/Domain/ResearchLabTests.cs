using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class ResearchLabTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Benchmark_compares_on_identical_holdout_and_reports_coverage()
    {
        BenchmarkPrediction[] rows = [Prediction(1, 10, 9, 8, 8, 12),
            Prediction(2, 10, 11, 8, 11, 12), Prediction(3, 10, 10, 8, null, null)];
        var result = ResearchBenchmark.Evaluate(rows, Now, Now.AddDays(5));
        result.Should().NotBeNull();
        result!.Model.MeanAbsoluteError.Should().BeApproximately(2.0 / 3, 0.000001);
        result.Baseline.MeanAbsoluteError.Should().Be(2);
        result.EmpiricalCoverage.Should().Be(0.5);
        result.IntervalCount.Should().Be(2);
        result.SessionCount.Should().Be(3);
    }

    [Fact]
    public void Benchmark_rejects_late_predictions_and_duplicate_sessions()
    {
        var row = Prediction(1, 10, 9, 8, null, null);
        ResearchBenchmark.Evaluate([row, row, Prediction(2, 10, 9, 8, null, null)],
            Now, Now.AddDays(5)).Should().BeNull();
        var late = row with { IssuedAt = row.TargetAt };
        ResearchBenchmark.Evaluate([late, Prediction(2, 10, 9, 8, null, null), Prediction(3, 10, 9, 8, null, null)],
            Now, Now.AddDays(5)).Should().BeNull();
    }

    [Fact]
    public void Walk_forward_rejects_overlapping_folds()
    {
        WalkForwardResearch.Evaluate([], [new(Now, Now.AddDays(3)), new(Now.AddDays(2), Now.AddDays(5))], 1)
            .Should().BeNull();
    }

    [Fact]
    public void Trial_uses_distinct_sessions_and_reports_paired_difference()
    {
        PairedTrialObservation[] pairs = [Pair(10, 12), Pair(10, 11), Pair(10, 13)];
        var result = PairedTrialEngine.Summarize(pairs);
        result.Should().NotBeNull();
        result!.MeanDifference.Should().Be(2);
        result.MedianDifference.Should().Be(2);
        result.SampleStandardDeviation.Should().Be(1);
        PairedTrialEngine.Summarize([pairs[0], pairs[0], pairs[2]]).Should().BeNull();
    }

    private static BenchmarkPrediction Prediction(int day, double observed, double model, double baseline,
        double? lower, double? upper)
    {
        var time = Now.AddDays(day);
        return new BenchmarkPrediction(Owner, Guid.NewGuid(), time, time.AddHours(1),
            time.AddHours(2), observed, model, baseline, lower, upper);
    }

    private static PairedTrialObservation Pair(double control, double intervention) =>
        new(Guid.NewGuid(), Owner, Guid.NewGuid(), Guid.NewGuid(), control, intervention);
}
