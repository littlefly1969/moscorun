namespace MoscoRun.Infrastructure.Persistence;

internal sealed class ScientificModelArtifactRow
{
    public Guid OwnerId { get; init; }

    public string ModelId { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string Format { get; init; } = string.Empty;

    public byte[] Artifact { get; init; } = [];

    public byte[] ArtifactHash { get; init; } = [];

    public string FeatureSchemaVersion { get; init; } = string.Empty;

    public DateTimeOffset TrainingCutoff { get; init; }

    public DateTimeOffset RegisteredAt { get; init; }

    public string ValidationJson { get; init; } = string.Empty;

    public string ConfigurationJson { get; init; } = string.Empty;
}
