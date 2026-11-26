namespace MoscoRun.Domain.Analysis;

public sealed record AdaptationModelSummary(FitnessFatigueParameters Parameters, FitnessFatigueState Initial,
    double Offset, double TrainingRmse, int TrainingCount, int CandidateCount, double BaselineMeanSpeed);
