namespace MoscoRun.Infrastructure.Persistence;

public sealed record ObservationInventoryGroup(string Metric, string Unit, string Source, string Method, Guid? DeviceId,
    bool Session, long RevisionRows, long NonNullRows, DateTimeOffset FirstTime, DateTimeOffset LastTime,
    DateTimeOffset LastAvailableAt);
