namespace MoscoRun.Domain.Analysis;

public sealed record EnvironmentalMetricSummary(string Metric, string Unit, double SupportedSeconds,
    double Coverage, double? WeightedMean);
