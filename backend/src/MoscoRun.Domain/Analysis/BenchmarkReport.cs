namespace MoscoRun.Domain.Analysis;

public sealed record BenchmarkReport(
    int SessionCount, int AthleteCount, BenchmarkScore Model, BenchmarkScore Baseline,
    double MeanAbsoluteErrorImprovement, int IntervalCount, double? EmpiricalCoverage, double? MeanIntervalWidth);
