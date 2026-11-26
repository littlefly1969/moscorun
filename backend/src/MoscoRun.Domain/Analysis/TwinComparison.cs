namespace MoscoRun.Domain.Analysis;

/// <summary>Confronto tra scenari sullo stesso orizzonte; non misura il beneficio causale.</summary>
public sealed record TwinComparison(double DistanceDifferenceMeters, double EndHeartRateDifferenceBpm, double MeanHeartRateDifferenceBpm);
