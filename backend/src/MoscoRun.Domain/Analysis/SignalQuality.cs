namespace MoscoRun.Domain.Analysis;

public sealed record SignalQuality(
    int SampleCount,
    int ValidCount,
    int InvalidCount,
    int OutsideCount,
    int DuplicateCount,
    int ConflictingCount,
    TimeSpan SupportedDuration,
    double Coverage,
    double? TimeWeightedMean);
