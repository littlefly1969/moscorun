namespace MoscoRun.Domain.Analysis;

public sealed record RunningDistanceReport(string MethodVersion, DateTimeOffset AsOf, DateTimeOffset PriorCutoff,
    DateOnly PriorFirstDay, DateOnly PriorLastDay, RunningDistanceEvidence Current, string Status,
    int CompleteDays, IReadOnlyList<DailyRunningDistancePoint> Days, IReadOnlyList<RunningDistanceEvidence> PriorRuns,
    double? PriorLongestMeters, double? IncreasePercent);
