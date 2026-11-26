namespace MoscoRun.Domain.Analysis;

public static class SleepDiaryEngine
{
    public const string MethodVersion = "main-sleep-local-clock-diary-v1";

    public static bool ValidWindow(DateOnly last, int days, DateTimeOffset asOf) =>
        days is >= 7 and <= 90 && last.DayNumber >= days && last < DateOnly.FromDateTime(asOf.UtcDateTime);

    public static SleepDiaryNight Night(DateOnly day, long? revision, DateTimeOffset? available,
        bool ambiguous, IReadOnlyDictionary<string, double?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var bed = values.GetValueOrDefault(SleepDiaryMetrics.Bedtime);
        var wake = values.GetValueOrDefault(SleepDiaryMetrics.Wake);
        var asleep = values.GetValueOrDefault("sleepDuration");
        var inBed = values.GetValueOrDefault(SleepDiaryMetrics.InBed);
        var end = new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        if (ambiguous || revision is not > 0 || available is null || available < end ||
            values.GetValueOrDefault(SleepDiaryMetrics.Confirmed) != 1)
        {
            return new SleepDiaryNight(day, ambiguous ? "ambiguous" : "unconfirmed", revision, available,
                null, null, null, null, null);
        }

        if (bed is null || wake is null || asleep is null || inBed is null ||
            !SleepDiaryMetrics.ValidValue(SleepDiaryMetrics.Bedtime, bed) ||
            !SleepDiaryMetrics.ValidValue(SleepDiaryMetrics.Wake, wake) ||
            !SleepDiaryMetrics.ValidValue(SleepDiaryMetrics.InBed, inBed))
        {
            return new SleepDiaryNight(day, "missing-or-invalid", revision, available, bed, wake, asleep, inBed, null);
        }

        var efficiency = RecoverySleepEngine.SleepEfficiencyPercent(asleep.Value / 60, inBed.Value / 60);
        return new SleepDiaryNight(day, efficiency is null ? "invalid-duration" : "observed", revision, available,
            bed, wake, asleep, inBed, efficiency);
    }

    public static SleepDiaryReport? Build(DateOnly last, int days, DateTimeOffset asOf, IReadOnlyList<SleepDiaryNight> nights)
    {
        ArgumentNullException.ThrowIfNull(nights);
        if (!ValidWindow(last, days, asOf) || nights.Count != days ||
            nights.Select((night, index) => night.Day == last.AddDays(index + 1 - days)).Any(valid => !valid) ||
            nights.Any(night => night.AvailableAt > asOf))
        {
            return null;
        }

        var complete = nights.Where(night => string.Equals(night.Status, "observed", StringComparison.Ordinal) &&
            night.BedtimeMinute.HasValue && night.WakeMinute.HasValue && night.EfficiencyPercent.HasValue).ToArray();
        var bedtime = SleepTimingEngine.Summarize(complete.Select(night => night.BedtimeMinute.GetValueOrDefault()).ToArray());
        var wake = SleepTimingEngine.Summarize(complete.Select(night => night.WakeMinute.GetValueOrDefault()).ToArray());
        return new SleepDiaryReport(MethodVersion, last, asOf, days, complete.Length,
            bedtime is null || wake is null ? "insufficient-timing-support" : "observed",
            nights, bedtime, wake, complete.Length >= 7 ? complete.Average(night => night.EfficiencyPercent) : null);
    }
}
