namespace MoscoRun.Domain.Analysis;

/// <summary>Fit su prove massimali attestate e calibrazione su blocco temporale successivo, senza riaddestramento.</summary>
public static class AttestedRaceEngine
{
    public const string Protocol = "flat-course-maximal-elapsed-v1";
    public const string AttestationMetric = "maximalFlatRun";
    public const double NominalCoverage = 0.8;

    public static bool ValidWindow(DateTimeOffset start, DateTimeOffset split, DateTimeOffset end,
        DateTimeOffset asOf, double targetMeters) =>
        start < split && split <= end && end <= asOf && end - start <= TimeSpan.FromDays(366) &&
        double.IsFinite(targetMeters) && targetMeters is >= 100 and <= 100000;

    public static AttestedRaceReport? Build(Guid owner, string source, DateTimeOffset start, DateTimeOffset split,
        DateTimeOffset end, DateTimeOffset asOf, double targetMeters, IReadOnlyList<AttestedRaceEffort> efforts)
    {
        ArgumentNullException.ThrowIfNull(efforts);
        if (owner == Guid.Empty || string.IsNullOrWhiteSpace(source) || !ValidWindow(start, split, end, asOf, targetMeters) ||
            efforts.Count > 2048 || efforts.Any(item => item.OwnerId != owner ||
                !string.Equals(item.Source, source, StringComparison.Ordinal) || !Valid(item)) ||
            efforts.Select(item => item.WorkoutId).Distinct().Count() != efforts.Count)
        {
            return null;
        }

        var rows = efforts.Where(item => item.StartedAt >= start && item.EndedAt < end &&
            item.AvailableAt <= asOf).OrderBy(item => item.StartedAt).ToArray();
        for (var index = 1; index < rows.Length; index++)
        {
            if (rows[index].StartedAt < rows[index - 1].EndedAt)
            {
                return null;
            }
        }

        var training = rows.Where(item => item.EndedAt < split && item.AvailableAt <= split).ToArray();
        var model = PersonalRaceCalibration.Fit(owner, Protocol, training.Select(ToCalibration), start, split);
        var prediction = model?.Predict(owner, Protocol, asOf, targetMeters);
        var evidence = rows.Select(item => Evidence(item, owner, model, split)).ToArray();
        var errors = evidence.Where(item => string.Equals(item.Role, "calibration", StringComparison.Ordinal) &&
            item.AbsoluteErrorSeconds.HasValue).Select(item => item.AbsoluteErrorSeconds.GetValueOrDefault());
        var interval = prediction is null ? null : RacePredictionEngine.Calibrate(prediction.PredictedSeconds, errors, NominalCoverage);
        var status = model is null ? "insufficient-training" : prediction is null ? "outside-distance-range" :
            interval is null ? "prediction-without-calibration" : "prediction-calibrated";
        var summary = model is null ? null : new RaceCalibrationModelSummary(model.MinimumDistanceMeters,
            model.MaximumDistanceMeters, model.ReferenceDistanceMeters, model.ReferenceSeconds, model.Exponent,
            model.MedianAbsoluteLogResidual, model.Count);
        return new AttestedRaceReport(source, Protocol, PersonalRaceCalibration.MethodVersion, asOf, start, split, end,
            targetMeters, status, summary, prediction, interval, Array.AsReadOnly(evidence),
            AttestedPerformanceEngine.Build(evidence));
    }

    private static RaceCalibrationEvidence Evidence(AttestedRaceEffort item, Guid owner, PersonalRaceModel? model,
        DateTimeOffset split)
    {
        var seconds = (item.EndedAt - item.StartedAt).TotalSeconds;
        var role = item.EndedAt < split
            ? item.AvailableAt <= split ? "training" : "late-training"
            : item.StartedAt >= split ? "calibration" : "cross-split";
        var predicted = string.Equals(role, "calibration", StringComparison.Ordinal)
            ? model?.Predict(owner, Protocol, split, item.DistanceMeters)?.PredictedSeconds : null;
        double? error = predicted is { } estimate ? Math.Abs(estimate - seconds) : null;
        return new RaceCalibrationEvidence(item.WorkoutId, item.Revision, item.ContentSha256, item.EndedAt,
            item.AvailableAt, item.FeedbackRevision, item.DistanceMeters, seconds, role, predicted, error, item.MeanPowerWatts);
    }

    private static RaceCalibrationEffort ToCalibration(AttestedRaceEffort item) =>
        new(item.OwnerId, item.WorkoutId, Protocol, item.EndedAt, item.AvailableAt, item.DistanceMeters,
            (item.EndedAt - item.StartedAt).TotalSeconds, true);

    private static bool Valid(AttestedRaceEffort item) =>
        item.WorkoutId != Guid.Empty && item.Revision >= 0 && item.FeedbackRevision > 0 &&
        item.EndedAt > item.StartedAt && item.AvailableAt >= item.EndedAt &&
        double.IsFinite(item.DistanceMeters) && item.DistanceMeters is >= 100 and <= 100000;
}
