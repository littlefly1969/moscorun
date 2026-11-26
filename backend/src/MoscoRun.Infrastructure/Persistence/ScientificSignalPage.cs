namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificSignalPage(IReadOnlyList<ScientificStoredSignalChunk> Items, ScientificSignalCursor? NextCursor);
