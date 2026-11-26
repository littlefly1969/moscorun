namespace MoscoRun.Domain.Analysis;

/// <summary>Statistiche descrittive di coppie non riutilizzate. Non certifica randomizzazione o causalità.</summary>
public static class PairedTrialEngine
{
    public static PairedTrialSummary? Summarize(IEnumerable<PairedTrialObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var pairs = observations.ToArray();
        if (pairs.Length < 3 || pairs.Any(pair => pair.OwnerId == Guid.Empty || pair.PairId == Guid.Empty ||
            pair.ControlSessionId == Guid.Empty || pair.InterventionSessionId == Guid.Empty ||
            !double.IsFinite(pair.Control) || !double.IsFinite(pair.Intervention)) ||
            pairs.Select(pair => pair.OwnerId).Distinct().Count() != 1 ||
            pairs.Select(pair => pair.PairId).Distinct().Count() != pairs.Length ||
            pairs.SelectMany(pair => new[] { pair.ControlSessionId, pair.InterventionSessionId })
                .Distinct().Count() != 2 * pairs.Length)
        {
            return null;
        }

        var differences = pairs.Select(pair => pair.Intervention - pair.Control).ToArray();
        var mean = differences.Average();
        var deviation = Math.Sqrt(differences.Sum(value => Math.Pow(value - mean, 2)) / (differences.Length - 1));
        var median = SignalProcessing.Median(differences);
        return double.IsFinite(mean) && double.IsFinite(deviation) && median.HasValue
            ? new PairedTrialSummary(pairs.Length, mean, median.Value, deviation) : null;
    }
}
