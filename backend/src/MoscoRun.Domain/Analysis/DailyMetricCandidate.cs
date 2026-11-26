namespace MoscoRun.Domain.Analysis;

public sealed record DailyMetricCandidate(ScientificObservation Observation, bool RevisionAmbiguous = false);
