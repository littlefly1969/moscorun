namespace MoscoRun.Domain.Analysis;

/// <summary>Verifica disponibilità sullo snapshot causale di un atleta e una sorgente.</summary>
public static class WearableCapabilityAudit
{
    public static WearableChannelAvailability GetAvailability(
        Guid ownerId,
        DateTimeOffset asOf,
        string source,
        string metric,
        IEnumerable<ScientificObservation> observations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(metric);
        var matching = ScientificSnapshot.Select(ownerId, asOf, observations)
            .Where(observation => observation.Source == source && observation.Metric == metric)
            .ToArray();

        if (matching.Any(observation => observation.Value.HasValue))
        {
            return WearableChannelAvailability.Observed;
        }

        return matching.Length > 0
            ? WearableChannelAvailability.ReportedMissing
            : WearableChannelAvailability.NotObserved;
    }
}
