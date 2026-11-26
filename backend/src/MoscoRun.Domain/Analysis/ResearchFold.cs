namespace MoscoRun.Domain.Analysis;

/// <summary>Fine training e termine della finestra di test (incluso tempo di disponibilità).</summary>
public sealed record ResearchFold(DateTimeOffset Cutoff, DateTimeOffset AsOf);
