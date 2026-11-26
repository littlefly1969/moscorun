namespace MoscoRun.Domain.Analysis;

public sealed record FitnessFatigueState(DateOnly Date, double Fitness, double Fatigue)
{
    public double NetResponse => Fitness - Fatigue;
}
