namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Campione temporale con precisione a 100 ns; null non equivale a zero.</summary>
public sealed record ScientificSignalSample(DateTimeOffset Time, double? Value);
