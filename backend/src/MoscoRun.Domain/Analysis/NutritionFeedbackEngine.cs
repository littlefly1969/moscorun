namespace MoscoRun.Domain.Analysis;

/// <summary>Contabilità descrittiva su totali dichiarati, senza prescrizioni o stime implicite.</summary>
public static class NutritionFeedbackEngine
{
    public const string MethodVersion = "declared-nutrition-accounting-v1";

    public static NutritionFeedbackReport Build(
        IReadOnlyDictionary<string, double?> values, bool session, double? elapsedHours,
        DateTimeOffset? availableAt, DateTimeOffset periodEnd)
    {
        ArgumentNullException.ThrowIfNull(values);
        var complete = availableAt >= periodEnd && Get(values, NutritionFeedbackMetrics.Complete) == 1;
        return session ? Session(values, complete, elapsedHours) : Daily(values, complete);
    }

    private static NutritionFeedbackReport Daily(IReadOnlyDictionary<string, double?> values, bool complete)
    {
        var exercise = Get(values, NutritionFeedbackMetrics.ExerciseNet);
        var intake = complete ? Get(values, "energyIntake") : null;
        var expenditure = complete ? EnergyAccounting.TotalExpenditure(Get(values, "restingEnergyExpenditure"),
            Get(values, NutritionFeedbackMetrics.Thermogenesis), Get(values, NutritionFeedbackMetrics.NonExerciseNet),
            exercise) : null;
        var balance = EnergyAccounting.EnergyBalance(intake, expenditure);
        var availability = EnergyAccounting.EnergyAvailability(intake, exercise, Get(values, "fatFreeMass"));
        return new NutritionFeedbackReport(MethodVersion, "athlete-feedback",
            Status(complete, expenditure, balance, availability), complete, null, expenditure, balance,
            availability, null, null, null, null);
    }

    private static NutritionFeedbackReport Session(IReadOnlyDictionary<string, double?> values, bool complete, double? hours)
    {
        var carbohydrate = complete ? Rate(Get(values, "carbohydrateIntake"), hours) : null;
        var fluid = complete ? Rate(Get(values, "fluidIntake"), hours) : null;
        var sodium = complete ? Rate(Get(values, "sodiumIntake"), hours) : null;
        var sweat = complete && Get(values, NutritionFeedbackMetrics.SweatProtocol) == 1 &&
            Get(values, NutritionFeedbackMetrics.BeforeWeight) is { } before &&
            Get(values, NutritionFeedbackMetrics.AfterWeight) is { } after &&
            Get(values, "fluidIntake") is { } consumed &&
            Get(values, NutritionFeedbackMetrics.Urine) is { } urine && hours is { } duration
            ? FuelingEngine.EstimatedSweatLitersPerHour(before, after, consumed / 1000, urine / 1000, duration)
            : null;
        return new NutritionFeedbackReport(MethodVersion, "athlete-feedback",
            Status(complete, carbohydrate, fluid, sodium), complete, hours, null, null, null,
            carbohydrate, fluid, sodium, sweat);
    }

    private static double? Get(IReadOnlyDictionary<string, double?> values, string metric) =>
        values.GetValueOrDefault(metric) is { } value && double.IsFinite(value) && value >= 0 ? value : null;

    private static double? Rate(double? amount, double? hours) =>
        amount is { } value && hours is { } duration && duration > 0 &&
        double.IsFinite(duration) && double.IsFinite(value / duration) ? value / duration : null;

    private static string Status(bool complete, params double?[] values) =>
        !complete ? "incomplete-diary" : values.All(value => value.HasValue) ? "observed" :
        values.Any(value => value.HasValue) ? "partial-inputs" : "missing-inputs";
}
