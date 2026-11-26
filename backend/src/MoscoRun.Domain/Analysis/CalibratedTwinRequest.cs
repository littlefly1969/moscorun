namespace MoscoRun.Domain.Analysis;

public sealed record CalibratedTwinRequest(WorkoutCardiacRecoveryProtocol RecoveryProtocol,
    double InitialBpm, double RestingBpm, double MaximumBpm, double MaximumFitRmseBpm,
    bool TransferAssumptionConfirmed, IReadOnlyList<TwinSegment> Segments);
