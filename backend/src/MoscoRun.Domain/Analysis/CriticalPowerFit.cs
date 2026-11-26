namespace MoscoRun.Domain.Analysis;

public sealed record CriticalPowerFit(double CriticalWatts, double WorkReserveJoules, double RootMeanSquaredErrorJoules, int EffortCount);
