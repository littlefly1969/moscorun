namespace MoscoRun.Infrastructure.Persistence;

internal sealed class ScientificSyncCheckpointRow
{
    public Guid OwnerId { get; init; }

    public string Source { get; init; } = string.Empty;

    public string Stream { get; init; } = string.Empty;

    public string Cursor { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
