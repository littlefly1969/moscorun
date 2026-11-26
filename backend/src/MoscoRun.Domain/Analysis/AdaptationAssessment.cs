namespace MoscoRun.Domain.Analysis;

public sealed record AdaptationAssessment(int Count, double MeanError, double MeanAbsoluteError, double RootMeanSquaredError);
