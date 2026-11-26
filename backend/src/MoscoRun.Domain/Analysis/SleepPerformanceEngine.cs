namespace MoscoRun.Domain.Analysis;

/// <summary>Associazione intra-atleta laggata, su prove omogenee; nessuna inferenza causale.</summary>
public static class SleepPerformanceEngine
{
    public const string MethodVersion = "lagged-sleep-attested-performance-pearson-v1";

    public static bool ValidWindow(DateOnly last, int days, int lag, DateTimeOffset asOf, double distance) =>
        lag is >= 1 and <= 7 && last.DayNumber >= days + lag &&
        SleepDiaryEngine.ValidWindow(last, days, asOf) && double.IsFinite(distance) && distance is >= 100 and <= 100000;

    public static SleepPerformanceReport? Build(string source, DateOnly last, int days, int lag, DateTimeOffset asOf,
        double distance, IReadOnlyList<SleepDiaryNight> nights, IReadOnlyList<RaceCalibrationEvidence> tests)
    {
        ArgumentNullException.ThrowIfNull(nights);
        ArgumentNullException.ThrowIfNull(tests);
        if (!ValidWindow(last, days, lag, asOf, distance) || string.IsNullOrWhiteSpace(source) ||
            nights.Select(item => item.Day).Distinct().Count() != nights.Count ||
            tests.Select(item => item.WorkoutId).Distinct().Count() != tests.Count)
        {
            return null;
        }

        var first = last.AddDays(1 - days);
        var candidates = tests.Where(item => double.IsFinite(item.DistanceMeters) &&
            Math.Abs(item.DistanceMeters - distance) <= 0.5 && double.IsFinite(item.DurationSeconds) &&
            item.DurationSeconds > 0 && item.DurationSeconds <= 604800 &&
            item.EndedAt <= item.AvailableAt && item.AvailableAt <= asOf)
            .Select(item => (Test: item, Start: item.EndedAt.AddSeconds(-item.DurationSeconds)))
            .Where(item => Day(item.Start) >= first && Day(item.Start) <= last).OrderBy(item => item.Start).ToArray();
        var groups = candidates.GroupBy(item => Day(item.Start)).ToDictionary(group => group.Key, group => group.Count());
        var byDay = nights.ToDictionary(item => item.Day);
        var evidence = candidates.Select(item => Pair(item.Test, item.Start, lag,
            groups[Day(item.Start)] > 1, byDay.GetValueOrDefault(Day(item.Start).AddDays(-lag)))).ToArray();
        var paired = evidence.Where(item => item.SleepHours.HasValue && item.SleepEfficiencyPercent.HasValue).ToArray();
        var speed = paired.Select(item => item.SpeedMetersPerSecond).ToArray();
        var hours = RecoverySleepEngine.SleepPerformanceCorrelation(paired.Select(item => item.SleepHours.GetValueOrDefault()).ToArray(), speed);
        var efficiency = Correlation(paired.Select(item => item.SleepEfficiencyPercent.GetValueOrDefault()).ToArray(), speed);
        return new SleepPerformanceReport(MethodVersion, source, asOf, last, days, lag, distance,
            paired.Length < 7 ? "insufficient-pairs" : hours is null && efficiency is null ? "constant-or-invalid-series" : "descriptive-association",
            paired.Length, hours, efficiency, Array.AsReadOnly(evidence));
    }

    private static SleepPerformancePair Pair(RaceCalibrationEvidence test, DateTimeOffset start, int lag,
        bool ambiguous, SleepDiaryNight? night)
    {
        var status = ambiguous ? "multiple-tests-per-day" :
            night is null || !string.Equals(night.Status, "observed", StringComparison.Ordinal) ? "missing-night" :
            night.AvailableAt is not { } available || available > start ? "late-night-revision" :
            night.AsleepSeconds is not { } asleep || !double.IsFinite(asleep) || asleep is < 0 or > 86400 ||
            night.EfficiencyPercent is not { } efficiency || !double.IsFinite(efficiency) || efficiency is < 0 or > 100
                ? "invalid-night" : "paired";
        var usable = string.Equals(status, "paired", StringComparison.Ordinal);
        return new SleepPerformancePair(test.WorkoutId, test.Revision, test.ContentSha256, test.FeedbackRevision,
            start, test.EndedAt, test.AvailableAt, test.DistanceMeters / test.DurationSeconds,
            Day(start).AddDays(-lag), night?.Revision, night?.AvailableAt, status,
            usable ? night?.AsleepSeconds / 3600 : null, usable ? night?.EfficiencyPercent : null);
    }

    private static double? Correlation(double[] percentages, double[] speeds) =>
        RecoverySleepEngine.SleepPerformanceCorrelation(percentages.Select(value => value / 100).ToArray(), speeds);

    private static DateOnly Day(DateTimeOffset time) => DateOnly.FromDateTime(time.UtcDateTime);
}
