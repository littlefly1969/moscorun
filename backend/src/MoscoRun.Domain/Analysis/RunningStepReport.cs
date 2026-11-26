namespace MoscoRun.Domain.Analysis;

public sealed record RunningStepReport(
    Guid WorkoutId, long Revision, string ContentSha256, string Source, DateTimeOffset AsOf,
    string MethodVersion, string Status, double MaxGapSeconds, bool CadenceIsStepsPerMinute,
    double DurationSeconds, double PairedSeconds, double Coverage,
    double? MeanSpeedMetersPerSecond, double? MeanCadenceStepsPerMinute,
    double? MeanStepLengthMeters, double? StepLengthCvPercent, double? CadenceCvPercent);
