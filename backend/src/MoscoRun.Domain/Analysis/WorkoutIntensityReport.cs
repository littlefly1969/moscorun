namespace MoscoRun.Domain.Analysis;

public sealed record WorkoutIntensityReport(
    Guid WorkoutId, long Revision, string ContentSha256, string Source, DateTimeOffset AsOf,
    string MethodVersion, double MaxGapSeconds, WorkoutIntensityProtocol Protocol, string Status, string Unit,
    IntensityDistribution Distribution, double? ObservedMean, double? SupportedHeartRateReserveLoad,
    double? SupportedExponentialTrimp, string HeartRateStatus, string TrimpMethodVersion);
