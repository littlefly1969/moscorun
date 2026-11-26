namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificStoredSignalChunk(Guid Id, DateTimeOffset AvailableAt, ScientificSignalChunk Chunk);
