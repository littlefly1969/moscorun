namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificModelArtifact(string ModelId, string Version, string Format, byte[] Artifact,
    string FeatureSchemaVersion, DateTimeOffset TrainingCutoff, string ValidationJson, string ConfigurationJson);
