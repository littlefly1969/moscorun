namespace MoscoRun.Domain.Analysis;

/// <summary>Predizioni concorrenti sulla medesima unità indipendente e prima dell'esito.</summary>
public sealed record BenchmarkPrediction(
    Guid OwnerId, Guid SessionId, DateTimeOffset IssuedAt, DateTimeOffset TargetAt,
    DateTimeOffset TargetAvailableAt, double Observed, double Model, double Baseline,
    double? Lower = null, double? Upper = null);
