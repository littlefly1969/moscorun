namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScaleInventoryGroup(Guid DeviceId, string Attribution, long Weighings, long WithImpedance,
    DateTimeOffset FirstTime, DateTimeOffset LastTime, DateTimeOffset LastReceivedAt);
