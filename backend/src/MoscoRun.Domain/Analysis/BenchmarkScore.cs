namespace MoscoRun.Domain.Analysis;

public sealed record BenchmarkScore(double Bias, double MeanAbsoluteError, double RootMeanSquaredError);
