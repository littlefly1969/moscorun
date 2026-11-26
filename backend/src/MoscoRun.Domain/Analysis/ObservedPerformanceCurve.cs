using MoscoRun.Domain.Workouts;

namespace MoscoRun.Domain.Analysis;

public sealed record ObservedPerformanceCurve(
    SeriesMetric Metric, string Unit, string MethodVersion, IReadOnlyList<ObservedPerformancePoint> Points);
