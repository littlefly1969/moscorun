namespace MoscoRun.Domain.Analysis;

public sealed record PairedTrialSummary(int PairCount, double MeanDifference, double MedianDifference, double SampleStandardDeviation);
