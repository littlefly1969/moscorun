namespace MoscoRun.Domain.Analysis;

public sealed record CalibratedTwinReport(string MethodVersion, DateTimeOffset AsOf, string Status,
    WorkoutCardiacRecoveryReport Calibration, CalibratedTwinRequest Request, TwinParameters? Parameters,
    IReadOnlyList<TwinSegmentResult> Segments);
