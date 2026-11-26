using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record WorkoutEnvironmentReport(Guid WorkoutId, long WorkoutRevision, string WorkoutContentSha256,
    DateTimeOffset AsOf, string Source, string WeatherStatus, WorkoutEnvironmentProtocol Protocol,
    bool Truncated, IReadOnlyList<BiomechanicsChunkMetadata> Chunks, IReadOnlyList<BiomechanicsChunkMetadata> Selected,
    WorkoutEnvironmentAnalysis Analysis);
