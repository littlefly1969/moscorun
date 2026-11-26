namespace MoscoRun.Domain.Analysis;

public sealed record BiomechanicalComparison(int MatchedSessions, double Current, double PriorMedian, double Difference);
