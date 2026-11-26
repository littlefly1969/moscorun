using MoscoRun.Domain.Analysis;

namespace MoscoRun.UnitTests.Domain;

public sealed class PersonalCalibrationTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly DateOnly First = new(2026, 10, 1);

    [Fact]
    public void Race_model_recovers_personal_exponent_and_respects_owner_time_and_distance()
    {
        var cutoff = At(First.AddDays(10));
        RaceCalibrationEffort[] rows = [
            Race(5000, 1), Race(10000, 2), Race(20000, 3),
            Race(15000, 4) with { OwnerId = Guid.NewGuid(), DurationSeconds = 1 },
            Race(12000, 5) with { AvailableAt = cutoff.AddDays(1), DurationSeconds = 1 },
            Race(17000, 11) with { DurationSeconds = 1 }];
        var model = PersonalRaceCalibration.Fit(Owner, "road", rows, At(First), cutoff);
        model.Should().NotBeNull();
        model!.Count.Should().Be(3);
        model.Exponent.Should().BeApproximately(1.1, 0.000001);
        var prediction = model.Predict(Owner, "road", cutoff, 15000);
        prediction.Should().NotBeNull();
        prediction!.PredictedSeconds.Should().BeApproximately(1200 * Math.Pow(3, 1.1), 0.000001);
        model.Predict(Owner, "road", cutoff, 42195).Should().BeNull();
        model.Predict(Owner, "road", cutoff.AddSeconds(-1), 10000).Should().BeNull();
        model.Predict(Guid.NewGuid(), "road", cutoff, 10000).Should().BeNull();
        model.Predict(Owner, "trail", cutoff, 10000).Should().BeNull();
    }

    [Fact]
    public void Race_calibration_requires_distinct_sessions_and_distance_range()
    {
        var row = Race(5000, 1);
        var cutoff = At(First.AddDays(10));
        PersonalRaceCalibration.Fit(Owner, "road", [row, row, Race(20000, 2)],
            At(First), cutoff).Should().BeNull();
        PersonalRaceCalibration.Fit(Owner, "road", [row, Race(5500, 2), Race(6000, 3)],
            At(First), cutoff).Should().BeNull();
    }

    [Fact]
    public void Fitness_fatigue_selects_individual_parameters_and_predicts_future_holdout()
    {
        var expected = new FitnessFatigueParameters(30, 5, 1, 2);
        var other = new FitnessFatigueParameters(30, 5, 1, 0.5);
        var initial = new FitnessFatigueState(First.AddDays(-1), 0, 0);
        var loads = CreateLoads();
        var points = CreatePoints(loads, expected, initial);
        var cutoff = At(First.AddDays(6));
        var fit = FitnessFatigueCalibration.Fit(Owner, loads, points, First, cutoff, initial, [other, expected]);
        fit.Should().NotBeNull();
        fit!.Parameters.Should().Be(expected);
        fit.Count.Should().Be(6);
        fit.Offset.Should().BeApproximately(100, 0.000001);
        fit.TrainingRmse.Should().BeApproximately(0, 0.000001);
        var holdout = fit.Predict(Owner, loads, points[^1].Time);
        holdout.Should().NotBeNull();
        holdout!.Value.Should().BeApproximately(points[^1].Response, 0.000001);
        fit.Predict(Owner, loads, cutoff.AddSeconds(-1)).Should().BeNull();
        fit.Predict(Guid.NewGuid(), loads, points[^1].Time).Should().BeNull();
    }

    [Fact]
    public void Calibration_rejects_revised_load_unavailable_when_the_outcome_occurred()
    {
        var parameters = new FitnessFatigueParameters(30, 5, 1, 2);
        var initial = new FitnessFatigueState(First.AddDays(-1), 0, 0);
        var loads = CreateLoads();
        var points = CreatePoints(loads, parameters, initial);
        loads[0] = loads[0] with { AvailableAt = points[1].Time };
        FitnessFatigueCalibration.Fit(Owner, loads, points, First, points[5].Time, initial, [parameters])
            .Should().BeNull();
    }

    private static RaceCalibrationEffort Race(double distance, int day) =>
        new(Owner, Guid.NewGuid(), "road", At(First.AddDays(day)), At(First.AddDays(day)),
            distance, 1200 * Math.Pow(distance / 5000, 1.1), true);

    private static DateTimeOffset At(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static DailyTrainingLoad[] CreateLoads()
    {
        double[] values = [10, 0, 30, 0, 20, 40, 0, 15];
        return values.Select((value, index) =>
            new DailyTrainingLoad(First.AddDays(index), At(First.AddDays(index + 1)), value)).ToArray();
    }

    private static FitnessFatigueCalibrationPoint[] CreatePoints(
        DailyTrainingLoad[] loads, FitnessFatigueParameters parameters, FitnessFatigueState initial)
    {
        return loads.Select(item =>
        {
            var time = At(item.Date.AddDays(1));
            var state = FitnessFatigueEngine.Replay(loads, First, item.Date, time, parameters, initial);
            return new FitnessFatigueCalibrationPoint(Owner, Guid.NewGuid(), time, time, 100 + state!.NetResponse);
        }).ToArray();
    }
}
