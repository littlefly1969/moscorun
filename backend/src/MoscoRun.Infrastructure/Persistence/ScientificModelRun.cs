namespace MoscoRun.Infrastructure.Persistence;

/// <summary>Decisione, target, ingressi, output con incertezza e stato del modello; non esegue il modello.</summary>
public sealed record ScientificModelRun(Guid Id, string ModelId, string Version, ScientificModelRunMode Mode,
    DateTimeOffset DecisionAt, DateTimeOffset? TargetAt, IReadOnlyList<Guid> InputIds,
    IReadOnlyList<ScientificModelOutput> Outputs, string? StateJson = null);
