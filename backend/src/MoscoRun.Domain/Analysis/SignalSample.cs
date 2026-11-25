namespace MoscoRun.Domain.Analysis;

public readonly record struct SignalSample(DateTimeOffset Time, double? Value);
