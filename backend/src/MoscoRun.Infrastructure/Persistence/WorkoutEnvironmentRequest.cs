using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record WorkoutEnvironmentRequest(string Source, IReadOnlyList<Guid> ChunkIds, WorkoutEnvironmentProtocol Protocol)
{
    public bool IsValid() => !string.IsNullOrWhiteSpace(Source) && Source.Length <= 200 &&
        ChunkIds is { Count: <= 4 } && ChunkIds.All(id => id != Guid.Empty) &&
        ChunkIds.Distinct().Count() == ChunkIds.Count && Protocol is not null && Protocol.IsValid();
}
