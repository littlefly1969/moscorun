namespace MoscoRun.Domain.Analysis;

public sealed record IntensityDistribution(
    IReadOnlyList<double> ZoneMinutes, double MissingMinutes, double TotalMinutes, double Coverage);
