namespace MoscoRun.Infrastructure.Persistence;

internal sealed class ScientificSignalChunkRow
{
    public Guid OwnerId { get; init; }

    public Guid Id { get; init; }

    public string Source { get; init; } = string.Empty;

    public string ExternalId { get; init; } = string.Empty;

    public string Revision { get; init; } = string.Empty;

    public long? RevisionOrder { get; init; }

    public string Metric { get; init; } = string.Empty;

    public string Unit { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string? ProviderAlgorithm { get; init; }

    public Guid? DeviceId { get; init; }

    public Guid? WorkoutId { get; init; }

    public long FirstTicks { get; init; }

    public long LastTicks { get; init; }

    public long AvailableTicks { get; init; }

    public int SampleCount { get; init; }

    public int CodecVersion { get; init; }

    public byte[] Data { get; init; } = [];

    public byte[] ContentHash { get; init; } = [];
}
