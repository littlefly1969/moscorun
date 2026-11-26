namespace MoscoRun.Domain.Analysis;

/// <summary>Confronto intra-atleta con matching esplicito per velocità, pendenza, fonte, metrica e unità.</summary>
public static class BiomechanicalContextEngine
{
    public static BiomechanicalComparison? Compare(
        BiomechanicalSession current, IEnumerable<BiomechanicalSession> history,
        DateTimeOffset asOf, double speedTolerance, double gradeTolerance)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(history);
        if (!Valid(current) || current.AvailableAt > asOf || !double.IsFinite(speedTolerance) ||
            !double.IsFinite(gradeTolerance) || speedTolerance < 0 || gradeTolerance < 0)
        {
            return null;
        }

        var matches = history.Where(item => Valid(item) && item.OwnerId == current.OwnerId &&
            item.SessionId != current.SessionId && item.Time < current.Time && item.AvailableAt <= current.Time &&
            string.Equals(item.Source, current.Source, StringComparison.Ordinal) &&
            string.Equals(item.Metric, current.Metric, StringComparison.Ordinal) &&
            string.Equals(item.Unit, current.Unit, StringComparison.Ordinal) &&
            Math.Abs(item.SpeedMetersPerSecond - current.SpeedMetersPerSecond) <= speedTolerance &&
            Math.Abs(item.GradePercent - current.GradePercent) <= gradeTolerance).ToArray();
        if (matches.Length < 3 || matches.Select(item => item.SessionId).Distinct().Count() != matches.Length)
        {
            return null;
        }

        var center = SignalProcessing.Median(matches.Select(item => item.Value));
        return center is { } value && double.IsFinite(current.Value - value)
            ? new BiomechanicalComparison(matches.Length, current.Value, value, current.Value - value) : null;
    }

    private static bool Valid(BiomechanicalSession item) =>
        item.OwnerId != Guid.Empty && item.SessionId != Guid.Empty && item.AvailableAt >= item.Time &&
        !string.IsNullOrWhiteSpace(item.Source) && !string.IsNullOrWhiteSpace(item.Metric) &&
        !string.IsNullOrWhiteSpace(item.Unit) && ScientificMetricCatalog.AcceptsUnit(item.Metric, item.Unit) &&
        double.IsFinite(item.SpeedMetersPerSecond) && item.SpeedMetersPerSecond > 0 &&
        double.IsFinite(item.GradePercent) && double.IsFinite(item.Value);
}
