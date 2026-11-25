namespace MoscoRun.Domain.Analysis;

/// <summary>Selezione temporale in memoria. Non sostituisce l'autorizzazione e la persistenza server.</summary>
public static class ScientificSnapshot
{
    public static IReadOnlyList<ScientificObservation> Select(
        Guid ownerId,
        DateTimeOffset asOf,
        IEnumerable<ScientificObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("Atleta obbligatorio.", nameof(ownerId));
        }

        var all = observations.ToArray();
        if (all.Any(observation => observation.OwnerId != ownerId))
        {
            throw new ArgumentException("Dataset contenente osservazioni di un altro atleta.", nameof(observations));
        }

        if (all.Select(observation => observation.Id).Distinct().Count() != all.Length)
        {
            throw new ArgumentException("Identificativi duplicati nel dataset.", nameof(observations));
        }

        // Una derivazione non entra nel replay prima della disponibilità di tutti gli ingressi.
        var available = all.Where(observation => observation.IsAvailableAt(asOf)).ToDictionary(observation => observation.Id);
        var accepted = new HashSet<Guid>();
        var pending = available.Values.OrderBy(observation => observation.AvailableAt).ThenBy(observation => observation.Id).ToArray();
        bool changed;
        do
        {
            changed = false;
            foreach (var observation in pending)
            {
                if (!accepted.Contains(observation.Id) &&
                    observation.InputIds.All(input => accepted.Contains(input) &&
                        available[input].AvailableAt <= observation.AvailableAt))
                {
                    changed |= accepted.Add(observation.Id);
                }
            }
        }
        while (changed);

        return Array.AsReadOnly(pending
            .Where(observation => accepted.Contains(observation.Id))
            .OrderBy(observation => observation.Time)
            .ThenBy(observation => observation.AvailableAt)
            .ThenBy(observation => observation.Id)
            .ToArray());
    }
}
