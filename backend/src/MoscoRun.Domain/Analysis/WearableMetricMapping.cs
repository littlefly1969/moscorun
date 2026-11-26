namespace MoscoRun.Domain.Analysis;

/// <summary>Mapping esplicito: non equipara unità native semanticamente ambigue.</summary>
public static class WearableMetricMapping
{
    public const string Version = "wearable-mapping-v1";

    public static ScientificObservation Normalize(
        Guid id,
        Guid ownerId,
        string nativeMetric,
        string nativeUnit,
        double? nativeValue,
        DateTimeOffset time,
        DateTimeOffset availableAt,
        string source)
    {
        // I nomi FIT prefissati provengono dal bundle Garmin già presente nel repository.
        // La cadenza FIT in rpm rimane nativa: manca la semantica passi/cicli.
        var (metric, unit, scale) = (source, nativeMetric, nativeUnit) switch
        {
            ("garmin-connect", "fit:verticalOscillation", "mm") => ("verticalOscillation", "m", 0.001),
            ("garmin-connect", "fit:stanceTime", "ms") => ("groundContactTime", "ms", 1.0),
            ("garmin-connect", "fit:verticalRatio", "%") => ("verticalRatio", "%", 1.0),
            ("garmin-connect", "fit:stepLength", "mm") => ("stepLength", "m", 0.001),
            ("garmin-connect", "fit:respirationRate", "1/min") => ("respirationRate", "1/min", 1.0),
            _ => (nativeMetric, nativeUnit, 1.0),
        };

        return new ScientificObservation(id, ownerId, metric, unit, nativeValue * scale,
            time, availableAt, source, ObservationMethod.ProviderReported);
    }
}
