namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificModelArtifactSnapshot(ScientificModelArtifact Definition, string Sha256, DateTimeOffset RegisteredAt);
