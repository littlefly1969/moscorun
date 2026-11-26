namespace MoscoRun.Domain.Analysis;

public sealed record RobustLineFit(double Intercept, double Slope, double MedianAbsoluteResidual, int Count);
