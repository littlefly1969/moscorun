namespace MoscoRun.Domain.Analysis;

public sealed record DailyLoadAttestation(DateOnly Day, double? Complete, DateTimeOffset? AvailableAt, bool Ambiguous);
