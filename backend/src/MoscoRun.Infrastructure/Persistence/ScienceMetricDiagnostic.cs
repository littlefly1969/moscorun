namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Contatori delle osservazioni mappate durante una singola esecuzione, non delle misure uniche.</summary>
public sealed record ScienceMetricDiagnostic(string Metric, string Permission, int Read, int Attempted,
    int Confirmed, int Rejected, int Conflicts);
