namespace MoscoRun.Domain.Analysis;

public static class SleepDiaryMetrics
{
    public const string Bedtime = "sleepBedtimeMinute";
    public const string Wake = "sleepWakeMinute";
    public const string InBed = "sleepInBedDuration";
    public const string Confirmed = "sleepNightConfirmed";

    public static bool IsDiaryMetric(string metric) => metric is Bedtime or Wake or InBed or Confirmed;

    public static bool ValidValue(string metric, double? value) => value is null || metric switch
    {
        Bedtime or Wake => value is >= 0 and < 1440 && value == Math.Truncate(value.Value),
        InBed => value is > 0 and <= 86400,
        Confirmed => value is 0 or 1,
        _ => true,
    };
}
