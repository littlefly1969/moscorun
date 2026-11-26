namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutDurabilityWindow(
    int OffsetSeconds, double PairedSeconds, double Coverage, double? MeanExternalLoad, double? MeanHeartRateBpm);
