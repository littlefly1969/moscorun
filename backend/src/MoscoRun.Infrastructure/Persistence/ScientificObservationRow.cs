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

    public Guid[] InputIds { get; init; } = [];
}
