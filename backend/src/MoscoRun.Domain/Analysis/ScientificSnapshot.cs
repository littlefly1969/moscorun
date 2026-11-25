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

        var available = all.Where(observation => observation.IsAvailableAt(asOf)).ToDictionary(observation => observation.Id);
        var accepted = ResolveLineage(available);

        return Array.AsReadOnly(available.Values
            .Where(observation => accepted.Contains(observation.Id))
            .OrderBy(observation => observation.Time)
            .ThenBy(observation => observation.AvailableAt)
            .ThenBy(observation => observation.Id)
            .ToArray());
    }

    private static HashSet<Guid> ResolveLineage(Dictionary<Guid, ScientificObservation> available)
    {
        var remaining = new Dictionary<Guid, int>();
        var dependents = new Dictionary<Guid, List<Guid>>();
        var ready = new Queue<Guid>();
        foreach (var observation in available.Values)
        {
            if (!observation.InputIds.All(input => available.TryGetValue(input, out var source) &&
                source.AvailableAt <= observation.AvailableAt))
            {
                continue;
            }

            remaining.Add(observation.Id, observation.InputIds.Count);
            if (observation.InputIds.Count == 0)
            {
                ready.Enqueue(observation.Id);
            }

            foreach (var input in observation.InputIds)
            {
                if (!dependents.TryGetValue(input, out var targets))
                {
                    targets = [];
                    dependents.Add(input, targets);
                }

                targets.Add(observation.Id);
            }
        }

        var accepted = new HashSet<Guid>();
        while (ready.TryDequeue(out var id))
        {
            accepted.Add(id);
            if (dependents.TryGetValue(id, out var targets))
            {
                foreach (var target in targets)
                {
                    if (--remaining[target] == 0)
                    {
                        ready.Enqueue(target);
                    }
                }
            }
        }

        return accepted;
    }
}
