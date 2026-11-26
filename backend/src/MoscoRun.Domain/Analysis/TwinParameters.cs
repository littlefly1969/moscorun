namespace MoscoRun.Domain.Analysis;

/// <summary>Parametri individuali espliciti, con riferimento al modello e al cutoff di calibrazione.</summary>
public sealed record TwinParameters(
    string CalibrationVersion, DateTimeOffset CalibrationCutoff, double HeartRateTimeConstantSeconds,
    double RestingHeartRateBpm, double MaximumHeartRateBpm);
