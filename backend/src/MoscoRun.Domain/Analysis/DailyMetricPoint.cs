namespace MoscoRun.Domain.Analysis;

public sealed record DailyMetricPoint(
    DateOnly Day, string Status, double? Value, Guid? ObservationId, DateTimeOffset? Time,
    DateTimeOffset? AvailableAt, string? Method, string? ProviderAlgorithm);
