namespace MoscoRun.Domain.Analysis;

public sealed record TerrainSummary(double SupportedSeconds, double HorizontalDistanceMeters,
    double? NetGradePercent, double? RawAscentMeters, double? RawDescentMeters);
