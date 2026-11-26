using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutChannelReport(
    SeriesMetric Metric, string Unit, string Status, int SampleCount, int ValidCount, int ConflictingCount,
    double Coverage, double? TimeWeightedMean);
