namespace MoscoRun.Infrastructure.Persistence;

internal sealed class ScientificLifecycleAuditRow
{
    public Guid OwnerId { get; init; }

    public Guid OperationId { get; init; }

    public string Action { get; init; } = string.Empty;

    public DateTimeOffset At { get; init; }

    public DateTimeOffset? Cutoff { get; init; }

    public string CountsJson { get; init; } = "{}";
}
