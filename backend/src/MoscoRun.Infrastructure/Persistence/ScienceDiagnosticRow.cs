namespace MoscoRun.Infrastructure.Persistence;

internal sealed class ScienceDiagnosticRow
{
    public Guid OwnerId { get; init; }

    public Guid DeviceId { get; init; }

    public string Source { get; init; } = string.Empty;

    public Guid RunId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public string Payload { get; set; } = string.Empty;
}
