namespace MoscoRun.Domain.Analysis;

public sealed record BiomechanicsMetricSummary(string Metric, string Unit, double SupportedSeconds, double Coverage,
    double? WeightedMean, double? WeightedCvPercent);
