namespace MoscoRun.Domain.Analysis;

public sealed record ObservedPerformancePoint(
    int DurationSeconds, double? Mean, DateTimeOffset? Start, DateTimeOffset? End);
