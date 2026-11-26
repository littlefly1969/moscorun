namespace MoscoRun.Infrastructure.Persistence;

internal sealed class ScientificLifecycleFenceRow
{
    public Guid OwnerId { get; init; }

    public DateTimeOffset BlockedAt { get; init; }
}
