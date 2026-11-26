namespace MoscoRun.Domain.Analysis;

public sealed record RepeatabilitySummary(int PairCount, double Bias, double TypicalError, double LowerAgreement, double UpperAgreement);
