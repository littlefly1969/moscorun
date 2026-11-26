namespace MoscoRun.Domain.Analysis;

public sealed record CardiacResponseFit(double TimeConstantSeconds, double RootMeanSquaredErrorBpm, int SampleCount);
