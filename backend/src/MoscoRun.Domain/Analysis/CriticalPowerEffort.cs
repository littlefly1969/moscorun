namespace MoscoRun.Domain.Analysis;

public sealed record CriticalPowerEffort(double DurationSeconds, double MeanWatts, bool IsMaximal);
