namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutEnvironmentAnalysis(string MethodVersion, double DurationSeconds,
    IReadOnlyList<EnvironmentalMetricSummary> Metrics, EnvironmentalExposure? HeatExposure, TerrainSummary Terrain);
