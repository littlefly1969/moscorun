namespace MoscoRun.Domain.Analysis;

/// <summary>Calibrazione condizionale e confronto temporale retrospettivo, non prova di causalità.</summary>
public static class PersonalAdaptationEngine
{
    public const string MethodVersion = "personal-adaptation-attested-session-rpe-v1";
    private static readonly FitnessFatigueParameters[] Grid =
        (from fitness in new double[] { 14, 28, 42, 60 }
         from fatigue in new double[] { 3, 7, 10 }
         from positive in new double[] { 0, 0.0001, 0.0005, 0.001 }
         from negative in new double[] { 0, 0.0001, 0.0005, 0.001 }
         select new FitnessFatigueParameters(fitness, fatigue, positive, negative)).ToArray();

    public static bool ValidWindow(DateOnly first, DateOnly split, DateOnly until, DateTimeOffset asOf, double distance) =>
        first > DateOnly.MinValue && first < split && split < until && until.DayNumber - first.DayNumber <= 180 &&
        until <= DateOnly.FromDateTime(asOf.UtcDateTime) && double.IsFinite(distance) && distance is >= 100 and <= 100000;

    public static PersonalAdaptationReport? Build(Guid owner, string source, DateOnly first, DateOnly split,
        DateOnly until, DateTimeOffset asOf, double distance, IReadOnlyList<DailySessionLoadPoint> history,
        IReadOnlyList<RaceCalibrationEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(evidence);
        if (owner == Guid.Empty || string.IsNullOrWhiteSpace(source) || !ValidWindow(first, split, until, asOf, distance) ||
            history.Count != until.DayNumber - first.DayNumber ||
            history.Select((point, index) => point.Day == first.AddDays(index)).Any(valid => !valid))
        {
            return null;
        }

        var loads = history.Select(point => new DailyTrainingLoad(point.Day, point.AvailableAt ?? asOf, point.Load)).ToArray();
        var rows = evidence.Where(item => Math.Abs(item.DistanceMeters - distance) <= 0.5 &&
                item.DurationSeconds > 0 && double.IsFinite(item.DurationSeconds))
            .Select(ToTest).OrderBy(item => item.StartedAt).ToArray();
        var cutoff = At(split);
        var training = rows.Where(item => string.Equals(item.Role, "training", StringComparison.Ordinal) &&
            item.StartedAt >= At(first.AddDays(1)) && item.EndedAt < cutoff && item.AvailableAt <= cutoff).ToArray();
        var initial = new FitnessFatigueState(first.AddDays(-1), 0, 0);
        var fit = FitnessFatigueCalibration.Fit(owner, loads, training.Select(item =>
            new FitnessFatigueCalibrationPoint(owner, item.WorkoutId, item.StartedAt, item.AvailableAt,
                item.ObservedSpeedMetersPerSecond)), first, cutoff, initial, Grid);
        var baseline = training.Length > 0 ? training.Average(item => item.ObservedSpeedMetersPerSecond) : (double?)null;
        var tests = rows.Select(item => Predict(item, owner, loads, cutoff, fit, baseline)).ToArray();
        var assessment = Evaluate(tests, cutoff, asOf, useBaseline: false);
        var baselineAssessment = Evaluate(tests, cutoff, asOf, useBaseline: true);
        var summary = fit is not null && baseline is { } mean ? new AdaptationModelSummary(fit.Parameters,
            fit.Initial, fit.Offset, fit.TrainingRmse, fit.Count, Grid.Length, mean) : null;
        var forecast = until == DateOnly.FromDateTime(asOf.UtcDateTime) ? fit?.Predict(owner, loads, asOf) : null;
        var status = fit is null ? "insufficient-causal-training" : assessment is null ? "insufficient-holdout" : "retrospective-holdout";
        return new PersonalAdaptationReport(MethodVersion, source, DailySessionLoadEngine.MethodVersion, first, split,
            until, asOf, distance, status, history, Array.AsReadOnly(tests), summary, assessment, baselineAssessment,
            forecast is > 0 ? forecast : null);
    }

    private static AdaptationTestEvidence ToTest(RaceCalibrationEvidence row) =>
        new(row.WorkoutId, row.Revision, row.ContentSha256, row.FeedbackRevision, row.EndedAt.AddSeconds(-row.DurationSeconds),
            row.EndedAt, row.AvailableAt, row.Role, row.DistanceMeters / row.DurationSeconds, null, null);

    private static AdaptationTestEvidence Predict(AdaptationTestEvidence item, Guid owner, DailyTrainingLoad[] loads,
        DateTimeOffset cutoff, FitnessFatigueFit? fit, double? baseline)
    {
        if (fit is null || item.StartedAt < cutoff || !string.Equals(item.Role, "calibration", StringComparison.Ordinal))
        {
            return item;
        }

        var predicted = fit.Predict(owner, loads, item.StartedAt);
        return item with
        {
            PredictedSpeedMetersPerSecond = predicted is > 0 ? predicted : null,
            BaselineSpeedMetersPerSecond = predicted is > 0 ? baseline : null,
        };
    }

    private static AdaptationAssessment? Evaluate(AdaptationTestEvidence[] tests, DateTimeOffset cutoff,
        DateTimeOffset asOf, bool useBaseline)
    {
        var eligible = tests.Where(item => item.PredictedSpeedMetersPerSecond.HasValue && item.BaselineSpeedMetersPerSecond.HasValue)
            .Select(item => new AdaptationObservation(item.EndedAt, item.AvailableAt, item.ObservedSpeedMetersPerSecond,
                (useBaseline ? item.BaselineSpeedMetersPerSecond : item.PredictedSpeedMetersPerSecond).GetValueOrDefault(),
                item.StartedAt));
        return AdaptationEngine.Evaluate(eligible, cutoff, asOf);
    }

    private static DateTimeOffset At(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
