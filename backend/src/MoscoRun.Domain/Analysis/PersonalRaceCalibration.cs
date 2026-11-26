namespace MoscoRun.Domain.Analysis;

/// <summary>Fit robusto log-tempo/log-distanza, esclusivamente su prove passate omogenee.</summary>
public static class PersonalRaceCalibration
{
    public const string MethodVersion = "personal-riegel-theil-sen-v1";

    public static PersonalRaceModel? Fit(
        Guid ownerId, string protocol, IEnumerable<RaceCalibrationEffort> efforts,
        DateTimeOffset trainingStart, DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(efforts);
        if (ownerId == Guid.Empty || string.IsNullOrWhiteSpace(protocol) || cutoff <= trainingStart)
        {
            return null;
        }

        var rows = efforts.Where(item => item.OwnerId == ownerId &&
            string.Equals(item.Protocol, protocol, StringComparison.Ordinal) && item.IsMaximal &&
            item.Time >= trainingStart && item.Time <= cutoff && item.AvailableAt <= cutoff).ToArray();
        if (rows.Length < 3 || rows.Length > 2048 ||
            rows.Any(item => item.SessionId == Guid.Empty || item.AvailableAt < item.Time ||
                !Positive(item.DistanceMeters) || !Positive(item.DurationSeconds)) ||
            rows.Select(item => item.SessionId).Distinct().Count() != rows.Length ||
            rows.Select(item => item.DistanceMeters).Distinct().Count() < 3)
        {
            return null;
        }

        var minimum = rows.Min(item => item.DistanceMeters);
        var maximum = rows.Max(item => item.DistanceMeters);
        if (maximum / minimum < 2)
        {
            return null;
        }

        // Centro in log-spazio: nessuna distanza di riferimento scelta dal test.
        var referenceLog = (Math.Log(minimum) + Math.Log(maximum)) / 2;
        var reference = Math.Exp(referenceLog);
        var x = rows.Select(item => Math.Log(item.DistanceMeters) - referenceLog).ToArray();
        var y = rows.Select(item => Math.Log(item.DurationSeconds)).ToArray();
        var fit = RobustLineRegression.Fit(x, y);
        if (fit is null || !double.IsFinite(fit.Slope) || fit.Slope < 1 || fit.Slope > 2 ||
            !double.IsFinite(fit.MedianAbsoluteResidual))
        {
            return null;
        }

        var seconds = Math.Exp(fit.Intercept);
        return Positive(reference) && Positive(seconds)
            ? new PersonalRaceModel(ownerId, protocol, trainingStart, cutoff, minimum, maximum, reference,
                seconds, fit.Slope, fit.MedianAbsoluteResidual, rows.Length)
            : null;
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
