namespace MoscoRun.Domain.Analysis;

/// <summary>Costanti di decadimento in giorni e guadagni per unità del medesimo carico.</summary>
public sealed record FitnessFatigueParameters(
    double FitnessTimeConstantDays, double FatigueTimeConstantDays, double FitnessGain, double FatigueGain);
