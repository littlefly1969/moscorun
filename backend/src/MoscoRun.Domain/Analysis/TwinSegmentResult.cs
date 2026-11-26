namespace MoscoRun.Domain.Analysis;

public sealed record TwinSegmentResult(
    double CumulativeSeconds, double CumulativeMeters, double EndHeartRateBpm, double MeanHeartRateBpm);
