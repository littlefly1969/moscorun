namespace MoscoRun.Domain.Analysis;

public sealed record SleepDiaryReport(string MethodVersion, DateOnly LastDay, DateTimeOffset AsOf,
    int WindowDays, int CompleteNights, string Status, IReadOnlyList<SleepDiaryNight> Nights,
    SleepTimingSummary? Bedtime, SleepTimingSummary? Wake, double? MeanEfficiencyPercent);
