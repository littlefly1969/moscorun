namespace MoscoRun.Domain.Analysis;

public sealed record SleepDiaryNight(DateOnly Day, string Status, long? Revision, DateTimeOffset? AvailableAt,
    double? BedtimeMinute, double? WakeMinute, double? AsleepSeconds, double? InBedSeconds,
    double? EfficiencyPercent);
