namespace MoscoRun.Domain.Analysis;

/// <summary>Carico giornaliero omogeneo e disponibile al momento del replay; zero è riposo osservato.</summary>
public sealed record DailyTrainingLoad(DateOnly Date, DateTimeOffset AvailableAt, double? Load);
