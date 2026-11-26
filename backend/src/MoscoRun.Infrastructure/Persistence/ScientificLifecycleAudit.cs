namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificLifecycleAudit(Guid OperationId, string Action, DateTimeOffset At,
    DateTimeOffset? Cutoff, ScientificLifecycleCounts Counts);
