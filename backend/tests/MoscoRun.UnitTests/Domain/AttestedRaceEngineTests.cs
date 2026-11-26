using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class AttestedRaceEngineTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Split = Start.AddDays(20);
    private static readonly DateTimeOffset End = Start.AddDays(40);

    [Fact]
    public void Fits_only_available_training_and_calibrates_on_later_in_range_tests()
    {
        var rows = new List<AttestedRaceEffort>
        {
            Effort(1, 1000), Effort(3, 2000), Effort(5, 4000),
            Effort(7, 2000, late: true),
        };
        rows.AddRange(Enumerable.Range(0, 5).Select(i => Effort(22 + i, 2000, error: 10)));
        rows.Add(Effort(30, 8000));
        var report = AttestedRaceEngine.Build(Owner, "test", Start, Split, End, End, 2000, rows)!;
        report.Status.Should().Be("prediction-calibrated");
        report.Model!.TrainingCount.Should().Be(3);
        report.Model.Exponent.Should().BeApproximately(1.1, 1e-7);
        report.Prediction!.PredictedSeconds.Should().BeApproximately(500 * Math.Pow(2, 0.1), 1e-6);
        report.Interval!.CalibrationCount.Should().Be(5);
        report.Interval.LowerSeconds.Should().BeApproximately(500 * Math.Pow(2, 0.1) - 10, 1e-6);
        report.Interval.UpperSeconds.Should().BeApproximately(500 * Math.Pow(2, 0.1) + 10, 1e-6);
        report.Evidence.Single(row => string.Equals(row.Role, "late-training", StringComparison.Ordinal))
            .AbsoluteErrorSeconds.Should().BeNull();
        report.Evidence.Single(row => row.DistanceMeters.Equals(8000)).PredictedSeconds.Should().BeNull();
        AttestedRaceEngine.Build(Owner, "test", Start, Split, End, End, 8000, rows)!.Prediction.Should().BeNull();
    }

    [Fact]
    public void Rejects_contamination_and_overlaps_and_refuses_insufficient_fit()
    {
        var row = Effort(1, 1000);
        AttestedRaceEngine.Build(Guid.NewGuid(), "test", Start, Split, End, End, 2000, [row]).Should().BeNull();
        AttestedRaceEngine.Build(Owner, "test", Start, Split, End, End, 2000,
            [row, row with { WorkoutId = Guid.NewGuid() }]).Should().BeNull();
        var report = AttestedRaceEngine.Build(Owner, "test", Start, Split, End, End, 2000, [row])!;
        report.Model.Should().BeNull();
        report.Prediction.Should().BeNull();
    }

    private static AttestedRaceEffort Effort(int day, double distance, bool late = false, double error = 0)
    {
        var time = Start.AddDays(day);
        var end = time.AddSeconds(250 * Math.Pow(distance / 1000, 1.1) + error);
        return new AttestedRaceEffort(Owner, Guid.NewGuid(), 1, "hash", "test", time, end,
            late ? Split.AddDays(1) : end.AddMinutes(5), 1, distance);
    }
}
