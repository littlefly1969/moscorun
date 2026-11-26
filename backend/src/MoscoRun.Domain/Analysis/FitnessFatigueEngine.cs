namespace MoscoRun.Domain.Analysis;

/// <summary>Modello impulso-risposta; stati latenti, non misure fisiologiche dirette.</summary>
public static class FitnessFatigueEngine
{
    public const string MethodVersion = "fitness-fatigue-discrete-v1";

    public static FitnessFatigueState? Replay(
        IReadOnlyList<DailyTrainingLoad> loads, DateOnly first, DateOnly last,
        DateTimeOffset asOf, FitnessFatigueParameters parameters, FitnessFatigueState initial)
    {
        ArgumentNullException.ThrowIfNull(loads);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(initial);
        if (last < first || first == DateOnly.MinValue || initial.Date != first.AddDays(-1) ||
            !ValidParameters(parameters) || !NonNegative(initial.Fitness) || !NonNegative(initial.Fatigue))
        {
            return null;
        }

        var eligible = loads.Where(item => item.Date >= first && item.Date <= last &&
            item.AvailableAt <= asOf).OrderBy(item => item.Date).ToArray();
        if (eligible.Length != last.DayNumber - first.DayNumber + 1)
        {
            return null;
        }

        return Advance(eligible, first, parameters, initial);
    }

    private static FitnessFatigueState? Advance(
        IReadOnlyList<DailyTrainingLoad> loads, DateOnly first,
        FitnessFatigueParameters parameters, FitnessFatigueState initial)
    {
        var fitness = initial.Fitness;
        var fatigue = initial.Fatigue;
        var fitnessDecay = Math.Exp(-1 / parameters.FitnessTimeConstantDays);
        var fatigueDecay = Math.Exp(-1 / parameters.FatigueTimeConstantDays);
        for (var index = 0; index < loads.Count; index++)
        {
            var item = loads[index];
            if (item.Date != first.AddDays(index) || item.Load is not { } load || !NonNegative(load))
            {
                return null;
            }

            fitness = fitness * fitnessDecay + parameters.FitnessGain * load;
            fatigue = fatigue * fatigueDecay + parameters.FatigueGain * load;
            if (!NonNegative(fitness) || !NonNegative(fatigue))
            {
                return null;
            }
        }

        return new FitnessFatigueState(loads[^1].Date, fitness, fatigue);
    }

    private static bool ValidParameters(FitnessFatigueParameters p) =>
        double.IsFinite(p.FitnessTimeConstantDays) && double.IsFinite(p.FatigueTimeConstantDays) &&
        p.FitnessTimeConstantDays > p.FatigueTimeConstantDays && p.FatigueTimeConstantDays > 0 &&
        NonNegative(p.FitnessGain) && NonNegative(p.FatigueGain);

    private static bool NonNegative(double value) => double.IsFinite(value) && value >= 0;
}
