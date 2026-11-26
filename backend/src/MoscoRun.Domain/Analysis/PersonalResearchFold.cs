namespace MoscoRun.Domain.Analysis;

public sealed record PersonalResearchFold(ResearchFold Fold, string Status, int TrainingCount, int TestCount,
    BenchmarkReport? Full, BenchmarkReport? Ablated);
