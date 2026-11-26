namespace MoscoRun.Domain.Analysis;

public sealed record AttestedRaceReport(string Source, string Protocol, string MethodVersion, DateTimeOffset AsOf,
    DateTimeOffset TrainingStart, DateTimeOffset Split, DateTimeOffset End, double TargetMeters, string Status,
    RaceCalibrationModelSummary? Model, RacePrediction? Prediction, PredictionInterval? Interval,
    IReadOnlyList<RaceCalibrationEvidence> Evidence, AttestedPerformanceProfile Performance);
