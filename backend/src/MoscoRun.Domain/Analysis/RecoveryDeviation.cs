namespace MoscoRun.Domain.Analysis;

public sealed record RecoveryDeviation(int BaselineCount, double BaselineMedian, double MedianAbsoluteDeviation, double? RobustZScore);
