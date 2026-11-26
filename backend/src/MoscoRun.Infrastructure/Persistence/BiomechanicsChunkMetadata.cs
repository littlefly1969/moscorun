namespace MoscoRun.Infrastructure.Persistence;

public sealed record BiomechanicsChunkMetadata(Guid Id, string Metric, string Unit, string ExternalId, string Revision,
    long? RevisionOrder, string Method, string? ProviderAlgorithm, Guid? DeviceId, DateTimeOffset FirstSample,
    DateTimeOffset LastSample, DateTimeOffset AvailableAt, int SampleCount, string ContentSha256);
