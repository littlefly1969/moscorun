namespace MoscoRun.Domain.Analysis;

/// <summary>Errore tipico e limiti di accordo descrittivi; le sedute non vengono riutilizzate.</summary>
public static class DurabilityRepeatability
{
    public static RepeatabilitySummary? Assess(IReadOnlyList<DurabilityRepeat> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        if (pairs.Count < 3 || pairs.Any(pair => !Valid(pair)) ||
            pairs.Select(pair => (pair.OwnerId, pair.ProtocolId, pair.Source, pair.Metric)).Distinct().Count() != 1 ||
            pairs.SelectMany(pair => new[] { pair.TestSessionId, pair.RetestSessionId }).Distinct().Count() != pairs.Count * 2)
        {
            return null;
        }

        var differences = pairs.Select(pair => pair.RetestLossPercent - pair.TestLossPercent).ToArray();
        var bias = differences.Average();
        var sd = Math.Sqrt(differences.Sum(value => Math.Pow(value - bias, 2)) / (differences.Length - 1));
        return double.IsFinite(bias) && double.IsFinite(sd) && double.IsFinite(bias + 1.96 * sd) &&
            double.IsFinite(bias - 1.96 * sd)
            ? new RepeatabilitySummary(pairs.Count, bias, sd / Math.Sqrt(2), bias - 1.96 * sd, bias + 1.96 * sd)
            : null;
    }

    private static bool Valid(DurabilityRepeat pair) =>
        pair.OwnerId != Guid.Empty && pair.TestSessionId != Guid.Empty && pair.RetestSessionId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(pair.ProtocolId) && !string.IsNullOrWhiteSpace(pair.Source) &&
        pair.Metric is ExternalLoadMetric.Speed or ExternalLoadMetric.Power &&
        double.IsFinite(pair.TestLossPercent) && double.IsFinite(pair.RetestLossPercent);
}
