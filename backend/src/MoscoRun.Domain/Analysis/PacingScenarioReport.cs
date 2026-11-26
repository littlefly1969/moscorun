namespace MoscoRun.Domain.Analysis;

public sealed record PacingScenarioReport(string MethodVersion, DateTimeOffset AsOf, string Status, string TargetSource,
    double DistanceMeters, double? TargetSeconds, IReadOnlyList<PacingSegment> Segments,
    IReadOnlyList<PacingSplit> Splits, RacePrediction? Prediction, PredictionInterval? Interval,
    IReadOnlyList<RaceCalibrationEvidence> Evidence);
