namespace MoscoRun.Domain.Analysis;

public sealed record DailyRunningDistancePoint(DateOnly Day, string Status, int RunningSessions, double? TotalMeters,
    double? LongestMeters, DateTimeOffset? AttestationAvailableAt);
