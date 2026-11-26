namespace MoscoRun.Domain.Analysis;

public sealed record PacingScenarioRequest(IReadOnlyList<PacingSegment> Segments, double? TargetSeconds,
    PacingRaceReference? RaceReference);
