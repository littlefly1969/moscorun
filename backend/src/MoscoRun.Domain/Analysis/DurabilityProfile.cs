namespace MoscoRun.Domain.Analysis;

/// <summary>Profilo descrittivo del deterioramento, con onset e trend sullo stesso protocollo di finestre.</summary>
public sealed record DurabilityProfile(
    DateTimeOffset BaselineStart,
    double BaselineEfficiency,
    IReadOnlyList<double> LossPercent,
    DateTimeOffset? Onset,
    DateTimeOffset? OnsetConfirmedAt,
    double LossPercentagePointsPerHour,
    double MedianAbsoluteResidualPercentagePoints);
