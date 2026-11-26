namespace MoscoRun.Domain.Analysis;

/// <summary>Confronto già abbinato in protocollo N-of-1; outcome orientato esplicitamente.</summary>
public sealed record PairedTrialObservation(
    Guid PairId, Guid OwnerId, Guid ControlSessionId, Guid InterventionSessionId,
    double Control, double Intervention);
