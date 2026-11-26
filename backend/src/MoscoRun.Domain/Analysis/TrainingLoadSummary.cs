namespace MoscoRun.Domain.Analysis;

public sealed record TrainingLoadSummary(double TotalLoad, double MeanDailyLoad, double? Monotony, double? Strain);
