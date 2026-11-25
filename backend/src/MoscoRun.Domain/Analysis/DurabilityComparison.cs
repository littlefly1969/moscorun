namespace MoscoRun.Domain.Analysis;

public sealed record DurabilityComparison(
    double EarlyEfficiency,
    double LateEfficiency,
    double EfficiencyLossPercent,
    double EarlyCoverage,
    double LateCoverage);
