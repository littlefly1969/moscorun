namespace MoscoRun.Domain.Analysis;

/// <summary>Confronto descrittivo di medie giornaliere in finestre disgiunte, non score di rischio.</summary>
public sealed record RollingLoadSummary(double RecentMean, double PriorMean, double? RecentToPriorRatio);
