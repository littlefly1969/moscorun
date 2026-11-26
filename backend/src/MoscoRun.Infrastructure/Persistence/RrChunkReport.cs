using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record RrChunkReport(string Source, DateOnly Day, DateTimeOffset AsOf, bool Truncated,
    IReadOnlyList<RrChunkMetadata> Chunks, RrChunkMetadata? Selected, RrChunkAnalysis? Analysis);
