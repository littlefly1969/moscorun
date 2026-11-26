namespace MoscoRun.Domain.Analysis;

/// <summary>Ricerca N-of-1 retrospettiva, con ablation su identico campione e due fold temporali prefissati.</summary>
public static class PersonalResearchEngine
{
    public const string MethodVersion = "personal-walk-forward-load-ablation-v1";
    private const int MinimumTraining = 10;
    private const int MinimumTest = 3;
    private static readonly string[] FeatureNames = ["priorSpeedMetersPerSecond", "recent7DayLoadMean", "prior21DayLoadMean"];

    public static bool ValidPenalty(double penalty) => double.IsFinite(penalty) && penalty is >= 0.0001 and <= 1000;

    public static PersonalResearchReport? Build(Guid owner, PersonalAdaptationReport input, double penalty)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (owner == Guid.Empty || !ValidPenalty(penalty) || input.UntilDay.DayNumber - input.SplitDay.DayNumber < 2 ||
            input.Loads.Select(item => item.Day).Distinct().Count() != input.Loads.Count ||
            input.Tests.Select(item => item.WorkoutId).Distinct().Count() != input.Tests.Count)
        {
            return null;
        }

        var evidence = Examples(input);
        var full = evidence.Select(item => Example(owner, item, true)).ToArray();
        var ablated = evidence.Select(item => Example(owner, item, false)).ToArray();
        var midpoint = input.SplitDay.AddDays((input.UntilDay.DayNumber - input.SplitDay.DayNumber) / 2);
        var folds = new[]
        {
            Evaluate(new ResearchFold(At(input.SplitDay), At(midpoint)), full, ablated, penalty),
            Evaluate(new ResearchFold(At(midpoint), At(input.UntilDay)), full, ablated, penalty),
        };
        var status = folds.All(item => item.Full is not null) ? "two-fold-retrospective" :
            folds.Any(item => item.Full is not null) ? "partial-fold-support" : "insufficient-fold-support";
        return new PersonalResearchReport(MethodVersion, RidgeRegression.MethodVersion, input.Source, input.AsOf,
            input.FirstDay, input.SplitDay, input.UntilDay, input.DistanceMeters, penalty, status,
            Array.AsReadOnly(FeatureNames), input.Tests.Count, Array.AsReadOnly(evidence), Array.AsReadOnly(folds));
    }

    private static ResearchExampleEvidence[] Examples(PersonalAdaptationReport input)
    {
        var days = input.Loads.ToDictionary(item => item.Day);
        var tests = input.Tests.OrderBy(item => item.StartedAt).ToArray();
        var evidence = new List<ResearchExampleEvidence>();
        foreach (var test in tests)
        {
            var day = DateOnly.FromDateTime(test.StartedAt.UtcDateTime);
            if (day.DayNumber - input.FirstDay.DayNumber < 28 || test.AvailableAt > input.AsOf ||
                test.StartedAt >= test.EndedAt || test.EndedAt > test.AvailableAt)
            {
                continue;
            }

            var previous = tests.Where(item => item.EndedAt < test.StartedAt && item.AvailableAt <= test.StartedAt)
                .MaxBy(item => item.EndedAt);
            if (previous is null)
            {
                continue;
            }

            var history = Enumerable.Range(1, 28).Select(offset => days.GetValueOrDefault(day.AddDays(-offset))).ToArray();
            if (history.Any(item => item?.Load is not { } load || !double.IsFinite(load) || load < 0 ||
                item.AvailableAt is not { } available || available > test.StartedAt))
            {
                continue;
            }

            var availableAt = history.Select(item => item!.AvailableAt!.Value).Append(previous.AvailableAt).Max();
            evidence.Add(new ResearchExampleEvidence(test.WorkoutId, previous.WorkoutId, test.StartedAt, availableAt,
                test.EndedAt, test.AvailableAt, previous.ObservedSpeedMetersPerSecond,
                history.Take(7).Average(item => item!.Load!.Value), history.Skip(7).Average(item => item!.Load!.Value),
                test.ObservedSpeedMetersPerSecond));
        }

        return evidence.ToArray();
    }

    private static LearningExample Example(Guid owner, ResearchExampleEvidence item, bool full) =>
        new(owner, item.WorkoutId, item.PredictionAt, item.FeaturesAvailableAt, item.TargetAt, item.TargetAvailableAt,
            full ? [item.PriorSpeedMetersPerSecond, item.RecentLoadMean, item.PriorLoadMean] : [item.PriorSpeedMetersPerSecond],
            item.ObservedSpeedMetersPerSecond);

    private static PersonalResearchFold Evaluate(ResearchFold fold, LearningExample[] full, LearningExample[] ablated,
        double penalty)
    {
        var split = LearningDataset.Split(full, fold.Cutoff, fold.AsOf);
        var training = split?.Training.Count ?? 0;
        var test = split?.Test.Count ?? 0;
        if (training < MinimumTraining || test < MinimumTest)
        {
            return new PersonalResearchFold(fold, "insufficient-support", training, test, null, null);
        }

        var first = WalkForwardResearch.Evaluate(full, [fold], penalty)?.Single().Report;
        var second = WalkForwardResearch.Evaluate(ablated, [fold], penalty)?.Single().Report;
        if (first is null || second is null || first.SessionCount != second.SessionCount)
        {
            return new PersonalResearchFold(fold, "invalid-fit", training, test, null, null);
        }

        return new PersonalResearchFold(fold, "paired-retrospective", training, test, first, second);
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
