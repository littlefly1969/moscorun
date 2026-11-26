namespace MoscoRun.Domain.Analysis;

public sealed record DailySessionLoadReport(
    DateOnly LastDay, DateTimeOffset AsOf, string MethodVersion, string Unit,
    IReadOnlyList<DailySessionLoadPoint> Days, TrainingLoadSummary? RecentWeek, RollingLoadSummary? Rolling);
