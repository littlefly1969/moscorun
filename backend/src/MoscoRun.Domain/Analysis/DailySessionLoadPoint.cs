namespace MoscoRun.Domain.Analysis;

public sealed record DailySessionLoadPoint(
    DateOnly Day, string Status, int Sessions, int RatedSessions, double? Load, DateTimeOffset? AvailableAt,
    DateTimeOffset? AttestationAvailableAt);
