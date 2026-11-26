namespace MoscoRun.Domain.Analysis;

public sealed record BiomechanicsChannel(string Metric, IReadOnlyList<SignalSample> Samples);
