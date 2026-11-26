namespace MoscoRun.Domain.Analysis;

public sealed record SleepPerformanceReport(string MethodVersion, string Source, DateTimeOffset AsOf,
    DateOnly LastTestDay, int WindowDays, int LagDays, double DistanceMeters, string Status,
    int PairedTests, double? SleepHoursCorrelation, double? SleepEfficiencyCorrelation,
    IReadOnlyList<SleepPerformancePair> Evidence);
