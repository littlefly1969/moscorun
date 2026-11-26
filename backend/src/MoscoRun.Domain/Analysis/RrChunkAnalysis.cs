namespace MoscoRun.Domain.Analysis;

public sealed record RrChunkAnalysis(string MethodVersion, string Context, string Status,
    int SampleCount, double ObservedSpanSeconds, double? IntervalDurationSeconds,
    string HrvMethodVersion, HrvSummary? Hrv, string DfaMethodVersion, DfaAlpha1Result? Dfa);
