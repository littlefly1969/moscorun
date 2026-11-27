namespace MoscoRun.Infrastructure.Persistence;

public sealed record InventoryDevice(Guid Id, string Name, DateTimeOffset? LastSeenAt, DateTimeOffset? RevokedAt);
