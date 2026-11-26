namespace MoscoRun.Infrastructure.Persistence;

internal sealed class ScientificModelRunRow
{
    public Guid OwnerId { get; init; }

    public Guid Id { get; init; }

    public string ModelId { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;

    public DateTimeOffset DecisionAt { get; init; }

    public DateTimeOffset? TargetAt { get; init; }

    public DateTimeOffset AvailableAt { get; init; }

    public Guid[] InputIds { get; init; } = [];

    public Guid[] OutputIds { get; init; } = [];

    public string OutputsJson { get; init; } = string.Empty;

    public string? StateJson { get; init; }
}
