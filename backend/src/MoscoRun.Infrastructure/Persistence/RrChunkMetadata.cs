namespace MoscoRun.Infrastructure.Persistence;

public sealed record RrChunkMetadata(Guid Id, string ExternalId, string Revision, long? RevisionOrder,
    string Unit, string Method, string? ProviderAlgorithm, Guid? WorkoutId, DateTimeOffset FirstSample,
    DateTimeOffset LastSample, DateTimeOffset AvailableAt, int SampleCount, string ContentSha256);
