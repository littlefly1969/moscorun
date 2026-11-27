namespace MoscoRun.Infrastructure.Persistence;

public sealed record InventoryObservation(Guid Id, string Metric, string Unit, double? Value, string Source,
    string Method, string? ProviderAlgorithm, Guid? DeviceId, Guid? WorkoutId, DateTimeOffset Time,
    DateTimeOffset AvailableAt, string? ExternalRevision, long? RevisionOrder, string? NativeMetric = null,
    string? NativeUnit = null, double? NativeValue = null, string? MappingVersion = null,
    bool? PossibleCrossSourceDuplicate = null);
