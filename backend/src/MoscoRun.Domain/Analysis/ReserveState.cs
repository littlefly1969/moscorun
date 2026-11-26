namespace MoscoRun.Domain.Analysis;

public sealed record ReserveState(double CumulativeSeconds, double Remaining, bool Exhausted);
