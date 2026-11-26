namespace MoscoRun.Domain.Analysis;

public sealed record RaceCalibrationModelSummary(double MinimumDistanceMeters, double MaximumDistanceMeters,
    double ReferenceDistanceMeters, double ReferenceSeconds, double Exponent, double MedianAbsoluteLogResidual,
    int TrainingCount);
