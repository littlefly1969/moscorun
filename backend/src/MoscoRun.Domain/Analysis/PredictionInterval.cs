namespace MoscoRun.Domain.Analysis;

/// <summary>Intervallo calibrato empiricamente sui residui disponibili; copertura non garantita in serie dipendenti.</summary>
public sealed record PredictionInterval(double LowerSeconds, double UpperSeconds, int CalibrationCount, double NominalCoverage);
