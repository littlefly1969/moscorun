namespace MoscoRun.Domain.Analysis;

public sealed record HrvSummary(int IntervalCount, double MeanRrMilliseconds, double RmssdMilliseconds, double SdnnMilliseconds);
