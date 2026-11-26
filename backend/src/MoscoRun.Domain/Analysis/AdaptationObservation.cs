namespace MoscoRun.Domain.Analysis;

/// <summary>Risposta osservata, orientata in modo che valori maggiori indichino prestazione maggiore.</summary>
public sealed record AdaptationObservation(DateTimeOffset Time, DateTimeOffset AvailableAt, double Response, double Predicted);
