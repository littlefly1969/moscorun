namespace MoscoRun.Domain.Measurements;

/// <summary>Pesata definitiva così come inviata da un agente di acquisizione.</summary>
/// <param name="SourceEventId">Identificativo stabile dell'evento generato dall'agente: chiave di idempotenza.</param>
/// <param name="MeasuredAt">Istante della misura, con offset.</param>
/// <param name="WeightKg">Peso in chilogrammi.</param>
/// <param name="ImpedanceOhm">Impedenza della stessa misura, se presente.</param>
public sealed record WeighingReading(
    string SourceEventId,
    DateTimeOffset MeasuredAt,
    decimal WeightKg,
    int? ImpedanceOhm);
