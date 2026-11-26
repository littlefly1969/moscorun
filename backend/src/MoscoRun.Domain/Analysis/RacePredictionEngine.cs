namespace MoscoRun.Domain.Analysis;

/// <summary>Baseline predittive trasparenti e confronto empirico. Non costituiscono prescrizione di gara.</summary>
public static class RacePredictionEngine
{
    public const string RiegelVersion = "riegel-explicit-exponent-v1";
    public const string CriticalSpeedVersion = "critical-speed-inverse-v1";

    public static RacePrediction? Riegel(
        double referenceMeters, double referenceSeconds, double targetMeters, double exponent)
    {
        if (!Positive(referenceMeters) || !Positive(referenceSeconds) || !Positive(targetMeters) ||
            !double.IsFinite(exponent) || exponent < 1 || exponent > 2)
        {
            return null;
        }

        var seconds = referenceSeconds * Math.Pow(targetMeters / referenceMeters, exponent);
        return Positive(seconds) ? new RacePrediction(targetMeters, seconds, RiegelVersion) : null;
    }

    public static RacePrediction? CriticalSpeed(CriticalSpeedFit fit, double targetMeters)
    {
        ArgumentNullException.ThrowIfNull(fit);
        if (!Positive(targetMeters) || !Positive(fit.CriticalSpeedMetersPerSecond) ||
            !Positive(fit.DistanceReserveMeters) || targetMeters <= fit.DistanceReserveMeters)
        {
            return null;
        }

        var seconds = (targetMeters - fit.DistanceReserveMeters) / fit.CriticalSpeedMetersPerSecond;
        return seconds >= PerformancePhysiology.MinimumDurationSeconds &&
            seconds <= PerformancePhysiology.MaximumDurationSeconds
            ? new RacePrediction(targetMeters, seconds, CriticalSpeedVersion) : null;
    }

    /// <summary>Quantile a rango finito di errori assoluti su set di calibrazione separato dal test.</summary>
    public static PredictionInterval? Calibrate(
        double predictedSeconds, IEnumerable<double> absoluteCalibrationErrors, double nominalCoverage)
    {
        ArgumentNullException.ThrowIfNull(absoluteCalibrationErrors);
        if (!Positive(predictedSeconds) || !double.IsFinite(nominalCoverage) ||
            nominalCoverage <= 0 || nominalCoverage >= 1)
        {
            return null;
        }

        var residuals = absoluteCalibrationErrors.Order().ToArray();
        if (residuals.Length < 5 || residuals.Any(error => !double.IsFinite(error) || error < 0))
        {
            return null;
        }

        var rank = (int)Math.Ceiling((residuals.Length + 1) * nominalCoverage);
        if (rank > residuals.Length)
        {
            return null;
        }

        var width = residuals[rank - 1];
        var upper = predictedSeconds + width;
        return double.IsFinite(upper)
            ? new PredictionInterval(Math.Max(0, predictedSeconds - width), upper, residuals.Length, nominalCoverage)
            : null;
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
}
