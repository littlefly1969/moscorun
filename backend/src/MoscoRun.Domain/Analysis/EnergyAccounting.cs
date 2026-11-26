namespace MoscoRun.Domain.Analysis;

/// <summary>Dispendio in kcal/giorno. Le componenti sono mutuamente esclusive, non calorie attive lorde.</summary>
public static class EnergyAccounting
{
    public const string MethodVersion = "energy-accounting-v1";

    public static double? TotalExpenditure(double? resting, double? thermogenesis, double? nonExerciseNet, double? exerciseNet)
    {
        double?[] components = [resting, thermogenesis, nonExerciseNet, exerciseNet];
        if (components.Any(value => !value.HasValue || !double.IsFinite(value.Value) || value.Value < 0) || resting <= 0)
        {
            return null;
        }

        var total = components.Sum(value => value.GetValueOrDefault());
        return double.IsFinite(total) ? total : null;
    }

    public static double? NetExerciseEnergy(double grossKcal, double restingKcalPerDay, double exerciseMinutes)
    {
        if (!double.IsFinite(grossKcal) || !double.IsFinite(restingKcalPerDay) ||
            !double.IsFinite(exerciseMinutes) || grossKcal < 0 || restingKcalPerDay <= 0 ||
            exerciseMinutes <= 0 || exerciseMinutes > 1440)
        {
            return null;
        }

        var net = grossKcal - restingKcalPerDay * (exerciseMinutes / 1440);
        return double.IsFinite(net) && net >= 0 ? net : null;
    }

    public static double? EnergyBalance(double? intake, double? expenditure) =>
        intake is { } consumed && expenditure is { } spent && double.IsFinite(consumed) &&
        double.IsFinite(spent) && consumed >= 0 && spent > 0 && double.IsFinite(consumed - spent)
            ? consumed - spent : null;

    /// <summary>Descrittore di disponibilità energetica, non una diagnosi di REDs.</summary>
    public static double? EnergyAvailability(double? intake, double? netExercise, double? fatFreeMassKg)
    {
        if (intake is not { } consumed || netExercise is not { } exercise || fatFreeMassKg is not { } mass ||
            !double.IsFinite(consumed) || !double.IsFinite(exercise) || !double.IsFinite(mass) ||
            consumed < 0 || exercise < 0 || mass <= 0)
        {
            return null;
        }

        var availability = (consumed - exercise) / mass;
        return double.IsFinite(availability) ? availability : null;
    }
}
