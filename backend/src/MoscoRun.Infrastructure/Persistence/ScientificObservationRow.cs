namespace MoscoRun.Infrastructure.Persistence;

// DTO di persistenza separato dal modello scientifico immutabile.
internal sealed class ScientificObservationRow
{
    public Guid OwnerId { get; init; }

    public Guid Id { get; init; }

    public string Metric { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public double? Value { get; init; }

    public DateTimeOffset Time { get; init; }

    public DateTimeOffset AvailableAt { get; init; }

    public string Source { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string? ProviderAlgorithm { get; init; }

    public string? DerivationVersion { get; init; }

    public string? ExternalId { get; init; }

    public string? ExternalRevision { get; init; }

    public long? ExternalRevisionOrder { get; init; }

    public Guid? ImportDeviceId { get; init; }

    public Guid? WorkoutId { get; init; }

    public string? NativeMetric { get; init; }

    public string? NativeUnit { get; init; }

    public double? NativeValue { get; init; }

    public string? MappingVersion { get; init; }

    public Guid[] InputIds { get; init; } = [];
}
