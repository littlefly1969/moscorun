using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class PersonalResearchEngineTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateOnly First = new(2026, 1, 1);
    private static readonly DateOnly Split = First.AddDays(55);
    private static readonly DateOnly Until = First.AddDays(80);
    private static readonly DateTimeOffset Now = At(Until).AddHours(12);

    [Fact]
    public void Compares_same_examples_on_two_disjoint_folds()
    {
        var input = Input();
        var report = PersonalResearchEngine.Build(Owner, input, 1)!;
        report.Status.Should().Be("two-fold-retrospective");
        report.Folds.Should().HaveCount(2);
        foreach (var fold in report.Folds)
        {
            fold.Full!.SessionCount.Should().Be(fold.Ablated!.SessionCount);
            fold.Full.Baseline.Should().Be(fold.Ablated.Baseline);
            fold.TrainingCount.Should().BeGreaterThanOrEqualTo(10);
        }

        report.Examples.Should().OnlyContain(item => item.FeaturesAvailableAt <= item.PredictionAt &&
            item.PredictionAt < item.TargetAt && item.TargetAt <= item.TargetAvailableAt);
        report.Examples.Should().OnlyContain(item => item.PriorWorkoutId != item.WorkoutId);
        report.Folds[0].Fold.AsOf.Should().Be(report.Folds[1].Fold.Cutoff);
    }

    [Fact]
    public void Missing_or_late_load_removes_examples_in_both_models()
    {
        var input = Input();
        var loads = input.Loads.ToArray();
        loads[30] = loads[30] with { Load = null, AvailableAt = null };
        var report = PersonalResearchEngine.Build(Owner, input with { Loads = loads }, 1)!;
        report.Examples.Should().NotContain(item => item.PredictionAt >= At(First.AddDays(31)) &&
            item.PredictionAt < At(First.AddDays(59)));
        PersonalResearchEngine.Build(Owner, input, 0).Should().BeNull();
    }

    private static PersonalAdaptationReport Input()
    {
        var loads = Enumerable.Range(0, 80).Select(index => new DailySessionLoadPoint(
            First.AddDays(index), "observed", 1, 1, 100 + index,
            At(First.AddDays(index + 1)), At(First.AddDays(index + 1)))).ToArray();
        var tests = Enumerable.Range(0, 40).Select(index =>
        {
            var start = At(First.AddDays(index * 2)).AddHours(12);
            var end = start.AddSeconds(250);
            return new AdaptationTestEvidence(Guid.NewGuid(), 1, "hash", 1, start, end, end.AddMinutes(1),
                "training", 4 + index * 0.01, null, null);
        }).ToArray();
        return new PersonalAdaptationReport("test", "test", DailySessionLoadEngine.MethodVersion, First, Split,
            Until, Now, 1000, "test", loads, tests, null, null, null, null);
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
