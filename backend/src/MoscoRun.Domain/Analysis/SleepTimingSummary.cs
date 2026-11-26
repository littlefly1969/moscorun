namespace MoscoRun.Domain.Analysis;

public sealed record SleepTimingSummary(int NightCount, double MeanMinuteOfDay, double CircularStandardDeviationMinutes);
