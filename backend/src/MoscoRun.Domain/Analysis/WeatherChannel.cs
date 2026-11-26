namespace MoscoRun.Domain.Analysis;

public sealed record WeatherChannel(string Metric, IReadOnlyList<SignalSample> Samples);
