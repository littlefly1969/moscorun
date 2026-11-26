namespace MoscoRun.Domain.Analysis;

public sealed record CardiacRecoveryPoint(int ElapsedSeconds, DateTimeOffset Target, DateTimeOffset? ObservedAt,
    double? HeartRateBpm, double? DropFromEndBpm);
