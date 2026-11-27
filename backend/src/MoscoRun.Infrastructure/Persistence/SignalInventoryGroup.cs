namespace MoscoRun.Infrastructure.Persistence;

public sealed record SignalInventoryGroup(string Metric, string Unit, string Source, string Method, Guid? DeviceId,
    bool Session, long ChunkRevisions, long StoredSamples, DateTimeOffset FirstTime, DateTimeOffset LastTime,
    DateTimeOffset LastAvailableAt);
