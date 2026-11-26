namespace MoscoRun.Domain.Analysis;

/// <summary>Una unità indipendente per seduta; caratteristiche causali e outcome distinto.</summary>
public sealed record LearningExample(
    Guid OwnerId,
    Guid SessionId,
    DateTimeOffset PredictionAt,
    DateTimeOffset FeaturesAvailableAt,
    DateTimeOffset TargetAt,
    DateTimeOffset TargetAvailableAt,
    IReadOnlyList<double> Features,
    double Target);
