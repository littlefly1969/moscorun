using MoscoRun.Domain.Analysis;
using MoscoRun.Domain.Workouts;
using MoscoRun.Infrastructure.Persistence;

namespace MoscoRun.Api.Features.Workouts;

internal static class ScienceFeedbackProtocol
{
    public const long MaxRevision = 1000000000;
    public const string LoadVersion = "session-rpe-active-minutes-v1";

    private static readonly KeyValuePair<string, string>[] DailyMetrics =
    [
        new("fatigue", "0-10"), new("stressPerceived", "0-10"), new("soreness", "0-10"),
        new("pain", "0-10"), new("sleepQuality", "0-10"), new("hunger", "0-10"),
        new("gastrointestinalDiscomfort", "0-10"), new("motivation", "0-10"),
        new("sleepDuration", "s"), new("napDuration", "s"),
        new(SleepDiaryMetrics.Bedtime, "min"), new(SleepDiaryMetrics.Wake, "min"),
        new(SleepDiaryMetrics.InBed, "s"), new(SleepDiaryMetrics.Confirmed, "1"),
        new("energyIntake", "kcal"), new("carbohydrateIntake", "g"), new("proteinIntake", "g"),
        new("fatIntake", "g"), new("fluidIntake", "mL"), new("sodiumIntake", "mg"), new("caffeineIntake", "mg"),
        new(ScientificFeedbackKeys.DailyCompleteMetric, "1"),
        new(NutritionFeedbackMetrics.Complete, "1"), new("restingEnergyExpenditure", "kcal"),
        new(NutritionFeedbackMetrics.Thermogenesis, "kcal"), new(NutritionFeedbackMetrics.NonExerciseNet, "kcal"),
        new(NutritionFeedbackMetrics.ExerciseNet, "kcal"), new("fatFreeMass", "kg"),
    ];

    private static readonly KeyValuePair<string, string>[] SessionMetrics =
    [
        new("rpe", "0-10"), new("fatigue", "0-10"), new("soreness", "0-10"),
        new("pain", "0-10"), new("gastrointestinalDiscomfort", "0-10"),
        new("carbohydrateIntake", "g"), new("fluidIntake", "mL"), new("sodiumIntake", "mg"),
        new("caffeineIntake", "mg"), new(NutritionFeedbackMetrics.Complete, "1"),
        new(NutritionFeedbackMetrics.BeforeWeight, "kg"), new(NutritionFeedbackMetrics.AfterWeight, "kg"),
        new(NutritionFeedbackMetrics.Urine, "mL"), new(NutritionFeedbackMetrics.SweatProtocol, "1"),
        new(AttestedRaceEngine.AttestationMetric, "1"), new(AttestedPerformanceEngine.PowerAttestation, "1"),
    ];

    public static bool ValidContext(DateOnly? day, Guid? workoutId, DateTimeOffset now) =>
        day is { } date
            ? workoutId is null && date >= new DateOnly(2000, 1, 1) && date <= DateOnly.FromDateTime(now.UtcDateTime)
            : workoutId is not null && workoutId != Guid.Empty;

    public static string EntryId(DateOnly? day, Workout? workout) =>
        workout is not null
            ? ScientificFeedbackKeys.Session(workout.Id, workout.Revision)
            : ScientificFeedbackKeys.Daily(day.GetValueOrDefault());

    public static DateTimeOffset ReferenceTime(DateOnly? day, Workout? workout) =>
        workout?.EndedAt ?? new DateTimeOffset(day.GetValueOrDefault().ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public static IReadOnlyList<AthleteFeedbackValue>? Values(bool isWorkout, IReadOnlyList<ScienceFeedbackInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var metrics = isWorkout ? SessionMetrics : DailyMetrics;
        if (inputs.Count > metrics.Length || inputs.Any(input => input is null ||
                !metrics.Any(metric => string.Equals(metric.Key, input.Metric, StringComparison.Ordinal))) ||
            inputs.Select(input => input.Metric).Distinct(StringComparer.Ordinal).Count() != inputs.Count)
        {
            return null;
        }

        var byMetric = inputs.ToDictionary(input => input.Metric, input => input.Value, StringComparer.Ordinal);
        var values = new List<AthleteFeedbackValue>(metrics.Length);
        foreach (var metric in metrics)
        {
            byMetric.TryGetValue(metric.Key, out var value);
            if (value is { } number && (!double.IsFinite(number) || number < 0 ||
                (NutritionFeedbackMetrics.IsAttestation(metric.Key) && number is not (0 or 1)) ||
                !SleepDiaryMetrics.ValidValue(metric.Key, number) ||
                (string.Equals(metric.Value, "0-10", StringComparison.Ordinal) && number > 10) || (string.Equals(metric.Value, "s", StringComparison.Ordinal) && number > 86400)))
            {
                return null;
            }

            values.Add(new AthleteFeedbackValue(metric.Key, metric.Value, value));
        }

        return values.AsReadOnly();
    }

    public static AthleteFeedbackSnapshot? Latest(IReadOnlyList<AthleteFeedbackSnapshot> history) =>
        history.Any(item => item.Entry.RevisionOrder is null) ||
        history.Select(item => item.Entry.RevisionOrder).Distinct().Count() != history.Count
            ? null
            : history.MaxBy(item => item.Entry.RevisionOrder);
}
