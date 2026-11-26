namespace MoscoRun.Domain.Analysis;

public sealed record ResearchExampleEvidence(Guid WorkoutId, Guid PriorWorkoutId, DateTimeOffset PredictionAt,
    DateTimeOffset FeaturesAvailableAt, DateTimeOffset TargetAt, DateTimeOffset TargetAvailableAt,
    double PriorSpeedMetersPerSecond, double RecentLoadMean, double PriorLoadMean, double ObservedSpeedMetersPerSecond);
