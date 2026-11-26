namespace MoscoRun.Domain.Analysis;

public sealed record PacingRaceReference(string Source, DateOnly From, DateOnly Split, DateOnly Until);
