using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class MachineLearningTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Ridge_recovers_linear_baseline_without_test_normalization()
    {
        LearningExample[] train = [Example(Owner, -5, 1, 5), Example(Owner, -4, 2, 7), Example(Owner, -3, 3, 9)];
        var model = RidgeRegression.Fit(train, Now, 0.000001);
        model.Should().NotBeNull();
        model!.Predict([4])!.Value.Should().BeApproximately(11, 0.00001);
        model.Predict([double.NaN]).Should().BeNull();
        model.Predict([1, 2]).Should().BeNull();
    }

    [Fact]
    public void Split_excludes_late_labels_and_holds_out_athlete()
    {
        var other = Guid.NewGuid();
        var delayed = Example(Owner, -3, 1, 2) with { TargetAvailableAt = Now.AddDays(1) };
        LearningExample[] all = [Example(Owner, -5, 1, 2), delayed, Example(other, -4, 1, 2),
            Example(other, 1, 2, 4), Example(Owner, 2, 2, 4)];
        var split = LearningDataset.Split(all, Now, Now.AddDays(5), other);
        split.Should().NotBeNull();
        split!.Training.Should().ContainSingle().Which.OwnerId.Should().Be(Owner);
        split.Test.Should().ContainSingle().Which.OwnerId.Should().Be(other);
    }

    [Fact]
    public void Duplicate_sessions_and_future_features_are_rejected()
    {
        var row = Example(Owner, -5, 1, 2);
        LearningDataset.Split([row, row, Example(Owner, 1, 2, 4)], Now, Now.AddDays(5)).Should().BeNull();
        var future = row with { FeaturesAvailableAt = row.PredictionAt.AddMinutes(1) };
        LearningDataset.Split([future, Example(Owner, 1, 2, 4)], Now, Now.AddDays(5)).Should().BeNull();
    }

    [Fact]
    public void Fit_refuses_test_labels_even_when_features_are_old()
    {
        var late = Example(Owner, -5, 1, 2) with { TargetAvailableAt = Now.AddDays(1) };
        RidgeRegression.Fit([late, Example(Owner, -4, 2, 4), Example(Owner, -3, 3, 6)], Now, 1)
            .Should().BeNull();
    }

    private static LearningExample Example(Guid owner, int day, double feature, double target)
    {
        var time = Now.AddDays(day);
        return new LearningExample(owner, Guid.NewGuid(), time, time, time.AddHours(1),
            time.AddHours(2), [feature], target);
    }
}
