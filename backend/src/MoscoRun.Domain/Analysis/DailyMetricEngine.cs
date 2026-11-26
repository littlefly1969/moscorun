namespace MoscoRun.Domain.Analysis;

/// <summary>Confronti su una sola osservazione giornaliera e un protocollo omogeneo; UTC esplicito.</summary>
public static class DailyMetricEngine
{
    public const string MethodVersion = "daily-observation-median-mad-v1";
    public const int MaxBaselineDays = 90;

    private static readonly Dictionary<string, string> Units = new(StringComparer.Ordinal)
    {
        ["restingHeartRate"] = "bpm",
        ["hrvRmssd"] = "ms",
        ["hrvSdnn"] = "ms",
        ["sleepDuration"] = "s",
        ["sleepEfficiency"] = "%",
        ["weight"] = "kg",
        ["fatPercentage"] = "%",
        ["fatMass"] = "kg",
        ["fatFreeMass"] = "kg",
        ["fatigue"] = "0-10",
        ["soreness"] = "0-10",
        ["sleepQuality"] = "0-10",
        ["stressPerceived"] = "0-10",
        ["motivation"] = "0-10",
    };

    public static bool IsSupported(string metric) => Units.ContainsKey(metric);

    public static bool IsValidWindow(DateOnly day, int baselineDays, DateTimeOffset asOf) =>
        baselineDays is >= 7 and <= MaxBaselineDays && day.DayNumber >= baselineDays &&
        day < DateOnly.FromDateTime(asOf.UtcDateTime);

    public static DailyMetricReport? Build(
        Guid ownerId, string metric, string source, DateOnly day, int baselineDays, DateTimeOffset asOf,
        IEnumerable<DailyMetricCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (ownerId == Guid.Empty || !Units.TryGetValue(metric, out var unit) ||
            string.IsNullOrWhiteSpace(source) || !IsValidWindow(day, baselineDays, asOf))
        {
            return null;
        }

        var rows = candidates.ToArray();
        if (rows.Any(row => row.Observation.OwnerId != ownerId))
        {
            return null;
        }

        var first = day.AddDays(-baselineDays);
        var byDay = rows.Where(row => Eligible(row.Observation, metric, source, asOf))
            .GroupBy(row => DateOnly.FromDateTime(row.Observation.Time.UtcDateTime))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var current = SelectDay(day, byDay, unit);
        var history = Enumerable.Range(0, baselineDays).Select(offset =>
            Compatible(SelectDay(first.AddDays(offset), byDay, unit), current)).ToArray();
        var observed = history.Where(point => point.Status == "observed").ToArray();
        var baseline = current.Value is { } value
            ? RecoverySleepEngine.CompareWithBaseline(value, observed.Select(point => point.Value.GetValueOrDefault()))
            : null;
        var change = baseline is not null && unit == "kg" && current.Value is { } mass
            ? BodyCompositionEngine.ChangePercent(mass, baseline.BaselineMedian)
            : null;
        var trend = string.Equals(metric, "weight", StringComparison.Ordinal) && baseline is not null
            ? WeightTrend(observed.Append(current).ToArray())
            : null;
        var status = current.Status != "observed" ? current.Status
            : baseline is null ? "insufficient-baseline" : "observed";
        return new DailyMetricReport(metric, unit, source, asOf, MethodVersion, baselineDays, current,
            Array.AsReadOnly(history), status, baseline, change, trend);
    }

    private static bool Eligible(ScientificObservation observation, string metric, string source, DateTimeOffset asOf) =>
        string.Equals(observation.Metric, metric, StringComparison.Ordinal) &&
        string.Equals(observation.Source, source, StringComparison.Ordinal) && observation.IsAvailableAt(asOf) &&
        observation.Method is (ObservationMethod.Measured or ObservationMethod.ProviderReported or
            ObservationMethod.ProviderEstimated or ObservationMethod.SelfReported) &&
        observation.InputIds.Count == 0;

    private static DailyMetricPoint SelectDay(
        DateOnly day, Dictionary<DateOnly, DailyMetricCandidate[]> byDay, string unit)
    {
        if (!byDay.TryGetValue(day, out var rows))
        {
            return new DailyMetricPoint(day, "missing", null, null, null, null, null, null);
        }

        if (rows.Length != 1 || rows[0].RevisionAmbiguous)
        {
            return new DailyMetricPoint(day, "ambiguous", null, null, null, null, null, null);
        }

        var observation = rows[0].Observation;
        var value = observation.Value;
        var status = value is null ? "missing" : ValidValue(value.Value, unit) ? "observed" : "invalid";
        return new DailyMetricPoint(day, status, status == "observed" ? value : null, observation.Id,
            observation.Time, observation.AvailableAt, observation.Method.ToString(), observation.ProviderAlgorithm);
    }

    private static bool ValidValue(double value, string unit) => double.IsFinite(value) && (unit switch
    {
        "0-10" => value is >= 0 and <= 10,
        "%" => value is >= 0 and <= 100,
        "s" => value is >= 0 and <= 86400,
        "ms" => value >= 0,
        _ => value > 0,
    });

    private static DailyMetricPoint Compatible(DailyMetricPoint point, DailyMetricPoint current) =>
        point.Status == "observed" && current.Status == "observed" && (
            !string.Equals(point.Method, current.Method, StringComparison.Ordinal) ||
            !string.Equals(point.ProviderAlgorithm, current.ProviderAlgorithm, StringComparison.Ordinal))
            ? point with { Status = "incompatible-method", Value = null }
            : point;

    private static double? WeightTrend(DailyMetricPoint[] points)
    {
        var first = points[0].Day.DayNumber;
        var x = points.Select(point => (double)(point.Day.DayNumber - first)).ToArray();
        var y = points.Select(point => point.Value.GetValueOrDefault()).ToArray();
        var fit = RobustLineRegression.Fit(x, y);
        var slope = fit?.Slope * 7;
        return slope is { } value && double.IsFinite(value) ? value : null;
    }
}
