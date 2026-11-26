namespace MoscoRun.Domain.Analysis;

public sealed record LearningSplit(IReadOnlyList<LearningExample> Training, IReadOnlyList<LearningExample> Test);
