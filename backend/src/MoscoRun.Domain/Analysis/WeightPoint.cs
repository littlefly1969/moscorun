namespace MoscoRun.Domain.Analysis;

/// <summary>Pesata ridotta ai dati usati dalle analisi.</summary>
public sealed record WeightPoint(DateTimeOffset MeasuredAt, decimal WeightKg);
