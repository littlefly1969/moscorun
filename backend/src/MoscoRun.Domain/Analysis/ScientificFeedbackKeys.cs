using System.Globalization;

namespace MoscoRun.Domain.Analysis;

public static class ScientificFeedbackKeys
{
    public const string DailyCompleteMetric = "trainingDayComplete";

    public static string Daily(DateOnly day) =>
        "web-daily:" + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Session(Guid workoutId, long revision) =>
        FormattableString.Invariant($"web-workout:{workoutId:N}:r{revision}");
}
