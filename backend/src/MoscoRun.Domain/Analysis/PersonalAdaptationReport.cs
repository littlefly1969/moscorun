namespace MoscoRun.Domain.Analysis;

public sealed record PersonalAdaptationReport(string MethodVersion, string Source, string LoadMethodVersion,
    DateOnly FirstDay, DateOnly SplitDay, DateOnly UntilDay, DateTimeOffset AsOf, double DistanceMeters,
    string Status, IReadOnlyList<DailySessionLoadPoint> Loads, IReadOnlyList<AdaptationTestEvidence> Tests,
    AdaptationModelSummary? Model, AdaptationAssessment? Holdout, AdaptationAssessment? BaselineHoldout,
    double? CurrentPredictedSpeedMetersPerSecond);
